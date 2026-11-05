using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 以 <c>-executeMethod Lexiscript.GameSetup.Run</c> 依 GameLevel.json 重建正式關卡：
    /// 由左而右一間間教學房間（出口門、答對計數、地板說明、暴雷條），最後是沒有提示的開放區。
    /// 手動改過 Game.unity 後就不要再執行，否則會被覆蓋；要改題目就改 json 再重建。
    /// </summary>
    public static class GameSetup
    {
        public const string ScenePath = SceneBuilder.Folder + "Game.unity";
        public const string LevelPath = SceneBuilder.Folder + "Editor/GameLevel.json";

#pragma warning disable 0649 // 欄位由 JsonUtility 填入
        [Serializable]
        sealed class LevelSpec
        {
            public RoomSpec[] rooms;
        }

        [Serializable]
        sealed class RoomSpec
        {
            public string key;
            public bool tutorial;
            public string[] texts;
            public GroupSpec[] groups;
            public int doorRequired;
        }

        [Serializable]
        sealed class GroupSpec
        {
            public BlockSpec[] blocks;
            public LinkSpec[] links;
            public string caption;
        }

        [Serializable]
        sealed class BlockSpec
        {
            public string id;
            public int dx, dy;
            public string color, shape, clue, answer;
            public string[] hints;
        }

        [Serializable]
        sealed class LinkSpec
        {
            public string from, to, kind;
        }
#pragma warning restore 0649

        sealed class RoomLayout
        {
            public RoomSpec Spec;
            public float Width, Height;
            /// <summary>門口那條線（世界座標 y = 0）在房間裡的高度，以房間頂端為 0、往下為負。</summary>
            public float Entry;
            /// <summary>以房間左上角為原點的位置。</summary>
            public readonly List<(BlockSpec spec, Vector2 position)> Blocks = new List<(BlockSpec, Vector2)>();
            /// <summary>name 是 Text（房間說明）或 Caption（題組說明）。</summary>
            public readonly List<(string name, string text, Vector2 position, float size)> Texts = new List<(string, string, Vector2, float)>();
        }

        const float SideMargin = 3f, TopMargin = 1.4f, TextSpacing = 0.85f, BottomMargin = 2f;
        // 門口那條線與上方說明文字（半高約 0.25）、下方第一排方塊各隔 EntryGap；沒有說明的房間，門口上方也至少留 DoorLintel 的牆
        const float EntryGap = 1.1f, TextHalfHeight = 0.25f, DoorLintel = 0.75f;
        const float TutorialRowHeight = 5f, OpenRowGap = 2.2f, CaptionGap = 0.55f, SpoilerGap = 0.45f;
        // 說明文字不換行，房間寬度依最長一行估算（LiberationSans 在 3.6 號字平均每字約 0.2）
        const float MinWidth = 18f, MinHeight = 10f, DoorHeight = 2.5f, CharWidth = 0.22f;
        const int MaxColumns = 7;
        const float TextFontSize = 3.6f, CaptionFontSize = 3f;

        public static void Run()
        {
            var level = JsonUtility.FromJson<LevelSpec>(File.ReadAllText(LevelPath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var material = SceneBuilder.Material();
            var layouts = level.rooms.Select(Layout).ToList();

            var floors = new List<Rect>();
            var doorways = new List<Rect>();
            float x = 0;
            for (int i = 0; i < layouts.Count; i++)
            {
                var room = layouts[i];
                // 所有門口都在 y = 0；房間依自己的 Entry 上下錯開，從門口進來時說明文字和第一排方塊都在畫面裡
                floors.Add(new Rect(x, -room.Entry - room.Height, room.Width, room.Height));
                if (i < layouts.Count - 1) doorways.Add(new Rect(x + room.Width, -DoorHeight / 2, Art.WallThickness, DoorHeight));
                x += room.Width + Art.WallThickness;
            }
            SceneBuilder.Rooms(material, floors, doorways, "GameRooms.asset", 7);

            var boardObject = SceneBuilder.BeginBoard();
            var guides = new GameObject("Guides").transform;
            var blocks = new Dictionary<string, Block>();
            var roomBlocks = new List<List<Block>>();
            for (int i = 0; i < layouts.Count; i++)
            {
                var origin = new Vector2(floors[i].xMin, floors[i].yMax);
                var inRoom = new List<Block>();
                foreach (var (spec, position) in layouts[i].Blocks)
                {
                    var block = SceneBuilder.Block(boardObject.transform, material, spec.id, origin + position,
                        Parse<BlockColor>(spec.color), Parse<BlockShape>(spec.shape), spec.clue ?? "", spec.answer ?? "", spec.hints ?? new string[0]);
                    blocks.Add(spec.id, block);
                    inRoom.Add(block);
                }
                foreach (var (name, text, position, size) in layouts[i].Texts)
                    SceneBuilder.FloorText(guides, name, text, origin + position, floors[i].width - 2 * SideMargin, size);
                roomBlocks.Add(inRoom);
            }
            foreach (var link in level.rooms.SelectMany(room => room.groups).SelectMany(group => group.links ?? new LinkSpec[0]))
                SceneBuilder.Link(blocks[link.from], blocks[link.to], Parse<LinkKind>(link.kind));
            var board = SceneBuilder.EndBoard(boardObject, material);

            for (int i = 0; i < layouts.Count; i++)
            {
                if (!layouts[i].Spec.tutorial) continue;
                var answerable = roomBlocks[i].Where(block => !string.IsNullOrEmpty(block.answer)).ToList();
                foreach (var block in answerable)
                    SceneBuilder.Spoiler(guides, material, block, (Vector2)block.transform.position + Vector2.down * (Art.Half + SpoilerGap));
                if (i < doorways.Count && layouts[i].Spec.doorRequired > 0)
                    SceneBuilder.Door(material, board, doorways[i], answerable, layouts[i].Spec.doorRequired);
            }

            // 第一間房正中間的門口那條線上：一開始就看得到上面的操作說明和下面的第一排方塊
            var spawn = new Vector2(floors[0].center.x, 0);
            SceneBuilder.Camera(spawn);
            SceneBuilder.Player(material, spawn);
            SceneBuilder.Hud();
            new GameObject("Progress", typeof(GameProgress));

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuilder.RegisterScenes();
            SceneBuilder.ConfigurePipeline();
        }

        /// <summary>
        /// 一間房的排版：上方是說明文字，接著是門口那條線，下面一排排放題組（一排最多 MaxColumns 格寬，題組之間空一格），
        /// 教學房間每格底下留暴雷條的位置，題組說明放在題組正下方。
        /// </summary>
        static RoomLayout Layout(RoomSpec spec)
        {
            var layout = new RoomLayout { Spec = spec };
            var texts = spec.texts ?? new string[0];
            var rows = new List<List<GroupSpec>>();
            int used = 0;
            foreach (var group in spec.groups)
            {
                int columns = Columns(group);
                if (rows.Count == 0 || used + 1 + columns > MaxColumns)
                {
                    rows.Add(new List<GroupSpec>());
                    used = -1;
                }
                rows[rows.Count - 1].Add(group);
                used += 1 + columns;
            }

            float RowWidth(List<GroupSpec> row) => (row.Sum(Columns) + row.Count - 2) * Art.Pitch + 2 * Art.Half;
            float textWidth = texts.Concat(spec.groups.Select(group => group.caption ?? "")).Select(text => text.Length * CharWidth).DefaultIfEmpty(0).Max();
            layout.Width = Mathf.Max(MinWidth, textWidth + 2 * SideMargin, rows.Select(RowWidth).DefaultIfEmpty(0).Max() + 2 * SideMargin);

            float y = -TopMargin;
            foreach (var text in texts)
            {
                layout.Texts.Add(("Text", text, new Vector2(layout.Width / 2, y), TextFontSize));
                y -= TextSpacing;
            }
            float textsBottom = texts.Length > 0 ? y + TextSpacing - TextHalfHeight : 0;
            layout.Entry = Mathf.Min(textsBottom - EntryGap, -(DoorHeight / 2 + DoorLintel));
            y = layout.Entry - EntryGap;

            foreach (var row in rows)
            {
                float left = (layout.Width - RowWidth(row)) / 2 + Art.Half;
                int height = row.Max(Rows);
                float lowest = y;
                foreach (var group in row)
                {
                    int minX = group.blocks.Min(block => block.dx), maxY = group.blocks.Max(block => block.dy);
                    foreach (var block in group.blocks)
                    {
                        var position = new Vector2(left + (block.dx - minX) * Art.Pitch, y - Art.Half - (maxY - block.dy) * Art.Pitch);
                        layout.Blocks.Add((block, position));
                        lowest = Mathf.Min(lowest, position.y);
                    }
                    if (!string.IsNullOrEmpty(group.caption))
                    {
                        float below = Art.Half + (spec.tutorial ? SpoilerGap + Spoiler.Size.y + CaptionGap : CaptionGap);
                        float center = left + (Columns(group) - 1) * Art.Pitch / 2;
                        float groupLowest = layout.Blocks.Where(placed => group.blocks.Contains(placed.spec)).Min(placed => placed.position.y);
                        layout.Texts.Add(("Caption", group.caption, new Vector2(center, groupLowest - below), CaptionFontSize));
                    }
                    left += (Columns(group) + 1) * Art.Pitch;
                }
                y -= spec.tutorial ? TutorialRowHeight * height : (height - 1) * Art.Pitch + 2 * Art.Half + OpenRowGap;
            }
            layout.Height = Mathf.Max(MinHeight, -y + BottomMargin);
            return layout;
        }

        static int Columns(GroupSpec group) => group.blocks.Max(block => block.dx) - group.blocks.Min(block => block.dx) + 1;

        static int Rows(GroupSpec group) => group.blocks.Max(block => block.dy) - group.blocks.Min(block => block.dy) + 1;

        static T Parse<T>(string value) where T : struct => (T)Enum.Parse(typeof(T), value);
    }
}
