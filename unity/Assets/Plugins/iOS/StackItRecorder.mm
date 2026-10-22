// Replay buffer for iOS: frames (BGRA, 720x1280) are H.264-encoded in real time with VideoToolbox
// and only the last N seconds of compressed samples are kept in memory. Export muxes those
// samples into an MP4 with AVAssetWriter in passthrough mode (no re-encode).
// Contract: Assets/_Project/Scripts/Services/Native/NativeBridge.cs (IosNative).

#import <AVFoundation/AVFoundation.h>
#import <CoreMedia/CoreMedia.h>
#import <VideoToolbox/VideoToolbox.h>

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

static VTCompressionSessionRef sSession = NULL;
static NSMutableArray* sSamples = nil; // CMSampleBufferRef, oldest first
static double sKeepSeconds = 18.0;
static int sWidth = 0;
static int sHeight = 0;

static bool StackItIsKeyframe(CMSampleBufferRef sample)
{
    CFArrayRef attachments = CMSampleBufferGetSampleAttachmentsArray(sample, false);
    if (attachments == NULL || CFArrayGetCount(attachments) == 0) return true;
    CFDictionaryRef dict = (CFDictionaryRef)CFArrayGetValueAtIndex(attachments, 0);
    return !CFDictionaryContainsKey(dict, kCMSampleAttachmentKey_NotSync);
}

static double StackItSeconds(id sample)
{
    return CMTimeGetSeconds(CMSampleBufferGetPresentationTimeStamp((__bridge CMSampleBufferRef)sample));
}

// Drops samples older than the window, then any leading non-keyframes (a clip must start on one).
static void StackItTrim(NSMutableArray* samples, double keepSeconds)
{
    if (samples.count == 0) return;
    double newest = StackItSeconds(samples.lastObject);
    while (samples.count > 1 && newest - StackItSeconds(samples.firstObject) > keepSeconds)
        [samples removeObjectAtIndex:0];
    while (samples.count > 0 && !StackItIsKeyframe((__bridge CMSampleBufferRef)samples.firstObject))
        [samples removeObjectAtIndex:0];
}

static void StackItEncoded(void* outputRefCon, void* frameRefCon, OSStatus status, VTEncodeInfoFlags flags, CMSampleBufferRef sample)
{
    if (status != noErr || sample == NULL || !CMSampleBufferDataIsReady(sample)) return;
    @synchronized (sSamples)
    {
        [sSamples addObject:(__bridge id)sample]; // the array retains the sample
        StackItTrim(sSamples, sKeepSeconds);
    }
}

extern "C" void _StackItRecStop(void)
{
    if (sSession != NULL)
    {
        VTCompressionSessionInvalidate(sSession);
        CFRelease(sSession);
        sSession = NULL;
    }
    if (sSamples != nil)
    {
        @synchronized (sSamples) { [sSamples removeAllObjects]; }
    }
}

extern "C" bool _StackItRecStart(int width, int height, int fps, int bitrate, int keepSeconds)
{
    _StackItRecStop();
    if (sSamples == nil) sSamples = [NSMutableArray array];
    sWidth = width;
    sHeight = height;
    sKeepSeconds = keepSeconds;

    NSDictionary* source = @{
        (id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
        (id)kCVPixelBufferWidthKey: @(width),
        (id)kCVPixelBufferHeightKey: @(height),
        (id)kCVPixelBufferIOSurfacePropertiesKey: @{},
    };
    OSStatus status = VTCompressionSessionCreate(kCFAllocatorDefault, width, height, kCMVideoCodecType_H264, NULL,
                                                 (__bridge CFDictionaryRef)source, NULL, StackItEncoded, NULL, &sSession);
    if (status != noErr) return false;

    VTSessionSetProperty(sSession, kVTCompressionPropertyKey_RealTime, kCFBooleanTrue);
    VTSessionSetProperty(sSession, kVTCompressionPropertyKey_ProfileLevel, kVTProfileLevel_H264_Main_AutoLevel);
    VTSessionSetProperty(sSession, kVTCompressionPropertyKey_AllowFrameReordering, kCFBooleanFalse);
    VTSessionSetProperty(sSession, kVTCompressionPropertyKey_AverageBitRate, (__bridge CFTypeRef)@(bitrate));
    VTSessionSetProperty(sSession, kVTCompressionPropertyKey_ExpectedFrameRate, (__bridge CFTypeRef)@(fps));
    VTSessionSetProperty(sSession, kVTCompressionPropertyKey_MaxKeyFrameInterval, (__bridge CFTypeRef)@(fps));
    return VTCompressionSessionPrepareToEncodeFrames(sSession) == noErr;
}

extern "C" void _StackItRecAppend(const void* bgra, int length, int bytesPerRow, double seconds)
{
    if (sSession == NULL || bgra == NULL) return;
    CVPixelBufferPoolRef pool = VTCompressionSessionGetPixelBufferPool(sSession);
    if (pool == NULL) return;
    CVPixelBufferRef pixels = NULL;
    if (CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, pool, &pixels) != kCVReturnSuccess) return;

    CVPixelBufferLockBaseAddress(pixels, 0);
    uint8_t* dst = (uint8_t*)CVPixelBufferGetBaseAddress(pixels);
    size_t dstStride = CVPixelBufferGetBytesPerRow(pixels);
    const uint8_t* src = (const uint8_t*)bgra;
    int rows = MIN(sHeight, length / bytesPerRow);
    for (int y = 0; y < rows; y++)
        memcpy(dst + y * dstStride, src + y * bytesPerRow, MIN((size_t)bytesPerRow, dstStride));
    CVPixelBufferUnlockBaseAddress(pixels, 0);

    VTCompressionSessionEncodeFrame(sSession, pixels, CMTimeMakeWithSeconds(seconds, 600), kCMTimeInvalid, NULL, NULL, NULL);
    CVPixelBufferRelease(pixels);
}

extern "C" void _StackItRecExport(const char* path, double lastSeconds)
{
    NSString* outPath = [NSString stringWithUTF8String:path];
    if (sSession != NULL) VTCompressionSessionCompleteFrames(sSession, kCMTimeInvalid);

    NSMutableArray* clip;
    @synchronized (sSamples) { clip = [sSamples mutableCopy]; }
    StackItTrim(clip, lastSeconds);

    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        void (^fail)(NSString*) = ^(NSString* reason) {
            UnitySendMessage("StackItNative", "OnClipExported", [[@"error:" stringByAppendingString:reason] UTF8String]);
        };
        if (clip.count == 0) { fail(@"empty"); return; }

        [[NSFileManager defaultManager] removeItemAtPath:outPath error:nil];
        NSError* error = nil;
        AVAssetWriter* writer = [AVAssetWriter assetWriterWithURL:[NSURL fileURLWithPath:outPath] fileType:AVFileTypeMPEG4 error:&error];
        if (writer == nil) { fail(error.localizedDescription ?: @"writer"); return; }

        CMSampleBufferRef first = (__bridge CMSampleBufferRef)clip.firstObject;
        AVAssetWriterInput* input = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeVideo
                                                                       outputSettings:nil
                                                                     sourceFormatHint:CMSampleBufferGetFormatDescription(first)];
        input.expectsMediaDataInRealTime = NO;
        [writer addInput:input];
        if (![writer startWriting]) { fail(writer.error.localizedDescription ?: @"start"); return; }
        [writer startSessionAtSourceTime:CMSampleBufferGetPresentationTimeStamp(first)];

        for (id sample in clip)
        {
            while (!input.readyForMoreMediaData) [NSThread sleepForTimeInterval:0.002];
            [input appendSampleBuffer:(__bridge CMSampleBufferRef)sample];
        }
        [input markAsFinished];
        [writer finishWritingWithCompletionHandler:^{
            if (writer.status == AVAssetWriterStatusCompleted)
                UnitySendMessage("StackItNative", "OnClipExported", [outPath UTF8String]);
            else
                fail(writer.error.localizedDescription ?: @"finish");
        }];
    });
}
