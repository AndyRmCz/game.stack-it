using System.Collections.Generic;
using StackIt.Client.Theming;
using UnityEngine;

namespace StackIt.UI
{
    /// <summary>Colours an <see cref="IsoTower"/> from the look: block.base for the tower, block.moving on top.</summary>
    public sealed class ThemedIsoTower : Themed
    {
        public IsoTower Target;
        public int Blocks = 3;
        public bool WithMoving = true;

        public override void Apply(ThemePalette p)
        {
            if (Target == null) return;
            Color[] faces = Look.Colors(T.BlockBase);
            var block = new IsoTower.BlockFaces { Top = faces[0], Left = faces[1], Right = faces[2] };
            var blocks = new List<IsoTower.BlockFaces>();
            for (int i = 0; i < Blocks; i++) blocks.Add(block);
            Target.SetBlocks(blocks);
            Color[] moving = Look.Colors(T.BlockMoving);
            Target.SetMoving(new IsoTower.BlockFaces { Top = moving[0], Left = moving[1], Right = moving[2] }, WithMoving);
        }
    }
}
