using System;
using System.Text;
using StackIt.Client;

// From unity/:  dotnet run --project DotNetTests/FaceDump
// Prints each pose the store art uses as "# Name" and then one line per quad FaceDrawing draws:
// the part, then x, y and alpha for each corner, in block widths from the foot's centre.

FacePose Moved(MascotMood mood, float lookX, float lookY)
{
    FacePose pose = FacePoses.For(mood);
    pose.LookX = lookX;
    pose.LookY = lookY;
    return pose;
}

var poses = new (string Name, FacePose Pose)[]
{
    ("Happy", FacePoses.For(MascotMood.Happy)),
    ("Joy", FacePoses.For(MascotMood.Joy)),
    ("Nervous", FacePoses.For(MascotMood.Nervous)),
    ("Rally", FacePoses.For(MascotMood.Rally)),
    // Looking up at the gold slab on its head, with a little "o": the icon's moment.
    ("Wonder", new FacePose
    {
        EyeOpen = 1.08f, LookX = 0.18f, LookY = 0.9f, PupilShrink = 0.1f, FaceShift = 0.008f,
        BrowRaise = 0.05f, BrowSkew = 0.01f, BrowTilt = 0.12f, BrowTwist = 0.02f,
        MouthShift = 0.01f, MouthWidth = 0.055f, MouthOpen = 0.1f, Blush = 0.7f, Squash = 0.03f,
    }),
    ("CheerUp", Moved(MascotMood.Cheer, 0.2f, 0.9f)),
    ("SmugRight", Moved(MascotMood.Smug, 0.7f, 0.35f)),
};

foreach (var (name, pose) in poses)
{
    var canvas = new Lines();
    FaceDrawing.Draw(pose, canvas);
    Console.WriteLine("# " + name);
    Console.Write(canvas.Text);
}

class Lines : IFaceCanvas
{
    private readonly StringBuilder _text = new StringBuilder();
    public string Text => _text.ToString();

    public void Quad(FacePart part, in FaceVertex a, in FaceVertex b, in FaceVertex c, in FaceVertex d) =>
        _text.AppendLine(FormattableString.Invariant(
            $"{part} {a.X} {a.Y} {a.Alpha} {b.X} {b.Y} {b.Alpha} {c.X} {c.Y} {c.Alpha} {d.X} {d.Y} {d.Alpha}"));
}
