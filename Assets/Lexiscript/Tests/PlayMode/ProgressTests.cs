using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lexiscript
{
    public sealed class ProgressTests
    {
        /// <summary>progress.json 的格式；測試直接寫檔，模擬舊版關卡留下的存檔。</summary>
        [System.Serializable]
        sealed class SaveFile
        {
            public int version = 1;
            public string[] solved = new string[0];
            public string[] firstLetter = new string[0];
            public string[] lastLetter = new string[0];
            public string[] spoilers = new string[0];
            public Vector2 player;
        }

        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        [UnityTest]
        public IEnumerator ProgressIsRestoredWhenTheGameIsLoadedAgain()
        {
            yield return LoadGame();
            Assert.IsNotNull(Object.FindAnyObjectByType<GameProgress>(), "Game 場景要有 GameProgress（由 GameSetup 建立）");
            var board = Object.FindAnyObjectByType<Board>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            var doors = Doors();
            Door exit = doors[0], next = doors[1];
            var solved = exit.blocks.Take(exit.required).ToArray();
            Block first = next.blocks[0], last = next.blocks[1], late = next.blocks[2];

            var spoiler = Spoilers().Single(s => s.block == solved[0]);
            yield return input.Click(InputHarness.Screen(spoiler.transform.position));
            Assert.IsTrue(spoiler.Revealed);
            Assert.IsFalse(File.Exists(input.ProgressPath), "變動後稍等一下才寫檔，不是每個變動都馬上寫");

            foreach (var block in solved) yield return Solve(board, block);
            yield return null;
            Assert.IsTrue(exit.IsOpen);

            // 門口那條線上沒有方塊；站在下一間房第二格的正上方，鏡頭照得到那一排
            var target = new Vector2(last.transform.position.x, exit.transform.position.y);
            body.position = target;
            yield return new WaitForFixedUpdate();
            yield return null;
            for (float until = Time.time + 2; RoomCover.Hides(first.transform.position) && Time.time < until;) yield return null;
            yield return input.Click(first, answerHalf: false);
            yield return input.Click(last, answerHalf: false, MouseButton.Right);
            Assert.AreEqual(Reveal.First, board.RevealOf(first));
            Assert.AreEqual(Reveal.Last, board.RevealOf(last));

            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(File.Exists(input.ProgressPath), "有變動就自動存檔");
            var json = File.ReadAllText(input.ProgressPath);
            foreach (var block in solved) StringAssert.Contains(block.name, json);

            // 這個變動還沒到存檔時間就換場景，要在卸載時補存
            yield return input.Click(late, answerHalf: false);
            Assert.AreEqual(Reveal.First, board.RevealOf(late));

            // 舊場景的物件換場景後就銷毀了，之後只能用 id 比對
            var solvedIds = solved.Select(block => block.name).ToArray();
            string firstId = first.name, lastId = last.name, lateId = late.name, spoilerId = solved[0].name;
            var position = body.position;
            var framing = solved.Concat(new[] { first, last, late }).Select(block => (Vector2)block.transform.position).Append(position).ToList();
            Shot("50_progress_before_reload", framing);

            var openings = new List<bool>();
            int solvedEvents = 0;
            // sceneLoaded 在新場景的 Awake/OnEnable 之後、Start 之前，趕得上讀檔時的開門與還原解題
            void Hook(Scene scene, LoadSceneMode mode)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var door in root.GetComponentsInChildren<Door>()) door.Opened += openings.Add;
                    foreach (var loaded in root.GetComponentsInChildren<Board>()) loaded.Solved += _ => solvedEvents++;
                }
            }
            SceneManager.sceneLoaded += Hook;
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            SceneManager.sceneLoaded -= Hook;

            board = Object.FindAnyObjectByType<Board>();
            doors = Doors();
            Assert.IsTrue(doors[0].IsOpen, "讀檔後門已經是開的");
            CollectionAssert.DoesNotContain(openings, false, "讀檔開的門不播開門動畫");
            var scale = doors[0].transform.localScale;
            Assert.Less(Mathf.Min(scale.x, scale.y), 0.01f, "門板一載入就是收起來的");
            Assert.AreEqual("", doors[0].counter.text);
            Assert.IsFalse(doors[1].IsOpen);
            Assert.AreEqual($"0/{doors[1].required}", doors[1].counter.text);
            Assert.AreEqual(0, solvedEvents, "還原解題不觸發 Board.Solved，不會播解題特效");

            Block Find(string id) => board.Blocks.Single(block => block.name == id);
            Assert.AreEqual(solvedIds.Length, board.Puzzle.SolvedCount);
            foreach (var id in solvedIds)
            {
                Assert.IsTrue(board.Puzzle.IsSolved(id), id);
                Assert.AreEqual(Find(id).answer, InputHarness.Parsed(Find(id).answerText), "已解開的答案欄顯示答案");
            }
            Assert.AreEqual(Reveal.First, board.RevealOf(Find(firstId)));
            Assert.AreEqual(Reveal.Last, board.RevealOf(Find(lastId)));
            Assert.AreEqual(Reveal.First, board.RevealOf(Find(lateId)), "換場景前最後的變動也有存到");
            Assert.AreEqual(AnswerHint.Placeholder(Find(firstId).answer, Reveal.First), InputHarness.Parsed(Find(firstId).answerText));
            Assert.AreEqual(AnswerHint.Placeholder(Find(lastId).answer, Reveal.Last), InputHarness.Parsed(Find(lastId).answerText));

            var revealed = Spoilers().Where(s => s.Revealed).ToArray();
            Assert.AreEqual(1, revealed.Length, "只有打開過的暴雷條是開的");
            Assert.AreEqual(spoilerId, revealed[0].block.name);
            Assert.AreEqual(revealed[0].block.answer, revealed[0].label.text);

            body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            Assert.Less(Vector2.Distance(position, body.position), 0.05f, "玩家回到存檔時的位置");
            // 角色有開插值，畫面上的位置要等兩次物理更新後才不再介於前後兩個位置之間
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.Less(Vector2.Distance(position, body.transform.position), 0.05f, "畫面上的角色也在存檔位置");
            Assert.IsFalse(RoomCover.Hides(position), "門已經開的房間沒有遮罩");
            Shot("51_progress_restored", framing);
        }

        [UnityTest]
        public IEnumerator UnreadableOrOutdatedSavesDoNotBreakTheGame()
        {
            File.WriteAllText(input.ProgressPath, "{ not json");
            yield return LoadGame();
            Assert.AreEqual(0, Object.FindAnyObjectByType<Board>().Puzzle.SolvedCount, "讀不懂的存檔就從頭開始");

            var block = Doors()[0].blocks[0];
            string id = block.name;
            var spawn = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>().position;
            // 模擬關卡改版後的舊存檔：有些 id 已經不存在，存的位置現在被方塊佔住
            File.WriteAllText(input.ProgressPath, JsonUtility.ToJson(new SaveFile
            {
                solved = new[] { "REMOVED_BLOCK", id },
                firstLetter = new[] { "REMOVED_BLOCK" },
                spoilers = new[] { "REMOVED_BLOCK" },
                player = block.transform.position,
            }));
            yield return LoadGame();

            var board = Object.FindAnyObjectByType<Board>();
            Assert.IsTrue(board.Puzzle.IsSolved(id), "還在的題目照樣還原");
            Assert.AreEqual(1, board.Puzzle.SolvedCount, "不存在的 id 直接略過");
            var exit = Doors()[0];
            Assert.AreEqual($"1/{exit.required}", exit.counter.text);
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            Assert.Less(Vector2.Distance(spawn, body.position), 0.05f, "存的位置卡在方塊裡，就留在出生點");
        }

        [UnityTest]
        public IEnumerator ResetAllClearsProgressAndNotesButKeepsSettings()
        {
            yield return LoadGame();
            GameSettings.Language = Language.Chinese;
            GameSettings.ShowSolvedCounter = false;
            var notes = Hud.Instance.Notes;
            var board = Object.FindAnyObjectByType<Board>();
            var exit = Doors()[0];
            notes.Buffer.Insert("white = the same word");
            yield return Solve(board, exit.blocks[0]);
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(File.Exists(input.ProgressPath));
            Assert.IsTrue(File.Exists(input.NotesPath));

            // 還沒到存檔時間的變動：重置後卸載場景時也不能被存回去
            yield return Solve(board, exit.blocks[1]);
            notes.Buffer.Insert(", black = reversed");
            GameProgress.ResetAll();
            Assert.IsFalse(File.Exists(input.ProgressPath), "刪掉進度");
            Assert.IsFalse(File.Exists(input.NotesPath), "刪掉筆記");
            Assert.AreEqual("", notes.Buffer.Text, "清空筆記");

            yield return null;
            yield return null;
            Assert.IsTrue(board == null, "換了場景");
            var expected = Application.CanStreamedLevelBeLoaded("Title") ? "Title" : "Game";
            Assert.AreEqual(expected, SceneManager.GetActiveScene().name, "有標題畫面就回標題畫面，否則重新開始這個場景");
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(File.Exists(input.ProgressPath), "重置前的進度沒有被存回去");
            Assert.IsFalse(File.Exists(input.NotesPath), "重置前的筆記沒有被存回去");
            GameSettings.Reload();
            Assert.AreEqual(Language.Chinese, GameSettings.Language, "設定保留");
            Assert.IsFalse(GameSettings.ShowSolvedCounter, "設定保留");
            if (expected != "Game") yield break;
            Assert.AreEqual(0, Object.FindAnyObjectByType<Board>().Puzzle.SolvedCount, "從頭開始");
            Assert.AreEqual("", Hud.Instance.Notes.Buffer.Text);
        }

        static IEnumerator LoadGame()
        {
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            // batchmode 的畫面很小，展開的筆記會蓋住右下角的方塊
            Hud.Instance.Notes.SetMinimized(true);
        }

        IEnumerator Solve(Board board, Block block)
        {
            yield return input.Click(block);
            input.Type(block.answer);
            Assert.IsTrue(board.Puzzle.IsSolved(block.name), block.name);
        }

        static Door[] Doors() => Object.FindObjectsByType<Door>(FindObjectsSortMode.None).OrderBy(door => door.transform.position.x).ToArray();

        static Spoiler[] Spoilers() => Object.FindObjectsByType<Spoiler>(FindObjectsSortMode.None);

        /// <summary>把這些點都框進畫面；重新載入前後用同一個框，兩張圖才好比對。</summary>
        static void Shot(string name, IReadOnlyCollection<Vector2> points)
        {
            var min = new Vector2(points.Min(p => p.x), points.Min(p => p.y)) - Vector2.one * 2.5f;
            var max = new Vector2(points.Max(p => p.x), points.Max(p => p.y)) + Vector2.one * 2.5f;
            InputHarness.Shot(name, (min + max) / 2, Mathf.Max(max.y - min.y, (max.x - min.x) * 9 / 16) / 2, hud: true);
        }
    }
}
