using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class xoScript: MonoBehaviour {

    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBombModule Module;

    public KMSelectable[] boardButtons, pageButtons;
    public MeshRenderer[] gridColors;
    public TextMesh inputTextMesh;
    public TextMesh[] gridTextMeshes;

    private const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ!";
    private const string pieces = "X O";
    private readonly string[] loggingPieces = {"X mark", "empty space", "O mark"};
    private readonly string[] locations = {"top-left", "top-middle", "top-right", "middle-left", "center", "middle-right",
        "bottom-left", "bottom-middle", "bottom-right"};
    private readonly string[] yesList = {"YEA", "YUP", "2EZ", "TKO", "WIN", "DUB"},
        noList = {"HUH", "HOW", "UHH", "404", "LOL", "RIP"};
    private const float animateDelay = 0.13f;
    private const float submitDelay = 0.5f;

    private string moduleKey, inputText, answer;
    private List<BoardSet> answerBoards = new List<BoardSet>();
    private List<List<BoardSet>> boardPool = new List<List<BoardSet>>(), backupBoardPool = new List<List<BoardSet>>();
    private List<string> wordBank;
    private int audioPointer = 7;
    private readonly List<string> audioNames = new List<string>() {"pagePressF#2", "pagePressF#3", "pagePressE2",
        "pagePressE3", "pagePressF#2", "pagePressF#3", "pagePressE2", "pagePressA3"};
    
    private int currentBoard, currentLetterPage, gameState; // 0-5: viewing board  6-8: submission pages 9: solved module 10: struck module
    private bool animateState;
    private char currentHighlightedLetter;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake () {
        moduleId = moduleIdCounter++; // version 1.0.2
        ModifyInputText("");
    }

    void Start () {
        wordBank = new List<string>() {"AIMING", "ANIMAL", "ANSWER", "APATHY", "ASYLUM", "AVENUE",
            "BEHEAD", "BOTHER", "BROKEN", "BUCKET", "BUNDLE", "BURIAL", "CHURCH", "CORNER", "CRISIS", "CROUCH", 
            "DOCTOR", "DRAGON", "DRAWER", "DRIVER", "EMBRYO", "EMPLOY", "ENERGY", "EXEMPT", 
            "FAMILY", "FATHER", "FILTER", "FLOWER", "FORMAL", "FORMAT", "FROZEN", "GARLIC", "GRAVEL", "GROUND", "GUITAR",
            "HARBOR", "HEAVEN", "HUNTER", "INJURY", "INVITE", "ISLAND", "JOCKEY", "JUNGLE", "JUNIOR",
            "KEYPAD", "KIDNEY", "LAUNCH", "LINEAR", "MAKEUP", "MARBLE", "MARINE", "MARKET", "MEADOW", "MEMBER", "METHOD", "MISERY",
            "NATIVE", "NUMBER", "PATENT", "PERIOD", "PLANET", "PLEASE", "PUBLIC", "PUNISH", "QUAINT",
            "REMAIN", "REMARK", "REMIND", "RESCUE", "RETAIN", "RETIRE", "REVEAL", "REVIEW", "REVISE", "REVIVE", "REVOKE", "RHYTHM", "ROTATE",
            "SAFARI", "SCHEME", "SCRAPE", "SERIAL", "SHADOW", "SINGLE", "SNATCH", "SWITCH", "SYMBOL", "TEMPLE", "THEORY", "THREAD", "TIPTOE",
            "UNFAIR", "UNIQUE", "UNLIKE", "UPDATE", "VOLUME", "WAITER", "WEALTH", "WORKER", "WINTER", "WRITER"
        }; // no two adjacent letters can have the same highlight
        InitializeBoardPool();
        
        for (int i = 0; i < 9; i++) boardButtons[i].OnInteract = BoardPress(i);
        for (int i = 0; i < 3; i++) pageButtons[i].OnInteract = PagePress(i);
        inputTextMesh.color = Color.white;

        answer = wordBank[UnityEngine.Random.Range(0, wordBank.Count)];
        int snKey = alphabet.IndexOf(Bomb.GetSerialNumber()[3]) - Bomb.GetSerialNumberNumbers().Sum();
        while (snKey < 0) snKey += 26;
        moduleKey = alphabet.Substring(snKey, 27 - snKey) + alphabet.Substring(0, snKey);
        moduleKey = moduleKey.Substring(14, 13) + moduleKey.Substring(0, 14);
        Debug.LogFormat("[XO #{0}] The grid is as follows:", moduleId);
        for (int i = 0; i < 3; i++) {
            Debug.LogFormat("[XO #{0}] {1} {2} {3}", moduleId, moduleKey.Substring(i * 9, 3),
                moduleKey.Substring(i * 9 + 3, 3), moduleKey.Substring(i * 9 + 6, 3));
        }

        boardPool.Shuffle();
        for (int i = 0; i < 6; i++) {
            backupBoardPool.Shuffle();
            int pointer = 0;
            int currentPosition = moduleKey.IndexOf(answer[i]) / 3, currentPiece = moduleKey.IndexOf(answer[i]) % 3;
            Debug.LogFormat("[XO #{0}] Grid #{1} ended with an {2} in the {3} cell.", moduleId, i + 1,
                loggingPieces[currentPiece], locations[currentPosition]);
            while (!IterateBoards(currentPosition, pieces[currentPiece], pointer)) pointer++;
        }
        
        Debug.LogFormat("[XO #{0}] The decrypted word is {1}.", moduleId, answer);
        currentBoard = UnityEngine.Random.Range(0, 6);
        currentLetterPage = 6;
        gameState = currentBoard;
        UpdateBoard();
    }

    // tries to find a valid board in the set and returns whether it was successful
    private bool IterateBoards(int pos, char symbol, int pointer) {
        bool spilled = pointer >= boardPool.Count;
        List<BoardSet> currentPool = (spilled ? backupBoardPool[pointer - boardPool.Count] : boardPool[pointer]).Shuffle();
        foreach (BoardSet board in currentPool){
            if (board.IsValid(pos, symbol)) {
                board.SetHighlight(pos);
                answerBoards.Add(board);
                if (!spilled) {
                    backupBoardPool.Add(boardPool[pointer]);
                    boardPool.Remove(boardPool[pointer]);
                }
                return true;
            }
        }
        return false;
    }

    private void InitializeBoardPool() {
        boardPool.Add(new List<BoardSet>() {
            new BoardSet("     OXX ", "X XOOOXXO"), // ... > X.X
            new BoardSet(" O   X  X", "XOO OXXOX"), // ..O > OOO
            new BoardSet(" XXO     ", "OXXOOXX O"), // XX. > XXO and its variations
            new BoardSet("X  X   O ", "XXOXO OOX"),
            new BoardSet("   O   XX", "X XOOOOXX"),
            new BoardSet("  X  X O ", "XOX OXXOO"),
            new BoardSet("XX   O   ", "XXOXOOO X"),
            new BoardSet(" O X  X  ", "OOXXO XXO")
        });
        boardPool.Add(new List<BoardSet>() {
            new BoardSet("   XOXO  ", "X XXOXOOO"), // ... > X.X
            new BoardSet(" X  O  XO", "XXO OXOXO"), // XOX > XOX
            new BoardSet("  OXOX   ", "OXOXOXX O"), // O.. > OOO and its variations
            new BoardSet("OX  O  X ", "OXXOO OXX"),
            new BoardSet("   XOX  O", "X XXOXOOO"),
            new BoardSet(" XO O  X ", "XXO OOXXO"),
            new BoardSet("O  XOX   ", "OXOXOXO X"),
            new BoardSet(" X  O OX ", "OXXXO OXO")
        });
        boardPool.Add(new List<BoardSet>() {
            new BoardSet("O     XXO", "OOX X XXO"), // O.. > OOX
            new BoardSet("  O  XO X", "O OXXXO X"), // ... > .X.
            new BoardSet("OXX     O", "OXX X OXO"), // XXO > XXO and its variations
            new BoardSet("X OX  O  ", "X OXXOO X"),
            new BoardSet("  O   OXX", "OXO X OXX"),
            new BoardSet("O X  X  O", "O XOXXX O"),
            new BoardSet("XXO   O  ", "XXO X OOX"),
            new BoardSet("O  X  X O", "O OXXXX O")
        });
        boardPool.Add(new List<BoardSet>() {
            new BoardSet(" X  X O  ", "OX XXOOOX"), // .X. > OX.
            new BoardSet("   XX   O", " OXXXOOXO"), // .X. > XXO
            new BoardSet("  O X  X ", "XOOOXX XO"), // O.. > OOX and its variations
            new BoardSet("O   XX   ", "OXOOXXXO "),
            new BoardSet(" X  X   O", " XOOXXXOO"),
            new BoardSet("  OXX    ", "OXOXXO OX"),
            new BoardSet("O   X  X ", "OOXXXOOX "),
            new BoardSet("    XXO  ", "XO OXXOXO")
        });
        boardPool.Add(new List<BoardSet>() {
            new BoardSet("XX      O", "XXO  X  O"), // XX. > XXO
            new BoardSet("  OX  X  ", "OXOX  X  "), // ... > ..X
            new BoardSet("O      XX", "O  X  OXX"), // ..O > ..O and its variations
            new BoardSet("  X  XO  ", "  X  XOXO"),
            new BoardSet(" XX   O  ", "OXXX  O  "),
            new BoardSet("X  X    O", "X  X  OXO"),
            new BoardSet("  O   XX ", "  O  XXXO"),
            new BoardSet("O    X  X", "OXO  X  X")
        });
        boardPool.Add(new List<BoardSet>() {
            new BoardSet("  X  OX  ", "OXXXOOXOX"), // ..X > OXX
            new BoardSet("XO      X", "XOXXOOOXX"), // ..O > XOO
            new BoardSet("  XO  X  ", "XOXOOXXXO"), // X.. > XOX and its variations
            new BoardSet("X      OX", "XXOOOXXOX"),
            new BoardSet("X  O    X", "XXOOOXXOX"),
            new BoardSet("  X   XO ", "OXXXOOXOX"),
            new BoardSet("X    O  X", "XOXXOOOXX"),
            new BoardSet(" OX   X  ", "XOXOOXXXO")
        });
        boardPool.Add(new List<BoardSet>() {
            new BoardSet(" OOX   X ", "XOOXXOOXX"), // .OO > XOO
            new BoardSet("O  O X X ", "OOXOXXXXO"), // X.. > XXO
            new BoardSet(" X   XOO ", "OXOXXXOOX"), // .X. > OXX and its variations
            new BoardSet(" X X O  O", "OXXXXOOXO"),
            new BoardSet("OO   X X ", "OOXOXXXXO"),
            new BoardSet(" X O XO  ", "XXOOXXOOX"),
            new BoardSet(" X X   OO", "OXOXXXXOO"),
            new BoardSet("  OX O X ", "OXOXXOOXX")
        });
        boardPool.Add(new List<BoardSet>() {
            new BoardSet("XO      X", "XOXXOOOXX"), // XO. > XOX
            new BoardSet("  XO  X  ", "XOXOOXXXO"), // ... > XOO
            new BoardSet("X      OX", "XXOOOXXOX"), // ..X > OXX and its variations
            new BoardSet("  X  OX  ", "OXXXOOXOX"),
            new BoardSet(" OX   X  ", "XOXOOXXXO"),
            new BoardSet("X  O    X", "XXOOOXXOX"),
            new BoardSet("  X   XO ", "OXXXOOXOX"),
            new BoardSet("X    O  X", "XOXXOOOXX")
        });
    }

    // handles interactions inside the board
    private KMSelectable.OnInteractHandler BoardPress(int i) {
        return delegate {
            boardButtons[i].AddInteractionPunch(.1f);

            if (gameState == 9 || animateState) {
                return false;
            }
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, boardButtons[i].transform);
            if (gameState == 8 && i == 8) {
                if (inputText.Length > 0) {
                    ModifyInputText(inputText.Substring(0, inputText.Length - 1));
                }
                currentHighlightedLetter = ' ';
                UpdateBoard();
            }
            else if (gameState >= 6) {
                currentHighlightedLetter = alphabet[(gameState - 6) * 9 + i];
                ModifyInputText(inputText + currentHighlightedLetter);
                UpdateBoard();
                if (inputText.Length == 6 && inputText == answer) {
                    gameState = 9;
                    StartCoroutine(AnimateWords(yesList.Shuffle().Take(3).ToList()));
                    Debug.LogFormat("[XO #{0}] The word {1} was submitted. Module solved.", moduleId, inputText);
                }
                else if (inputText.Length == 6 && inputText != answer) {
                    gameState = 10;
                    StartCoroutine(AnimateWords(noList.Shuffle().Take(3).ToList()));
                    Debug.LogFormat("[XO #{0}] The word {1} was submitted. Strike.", moduleId, inputText);
                }
            }
            return false;
        };
    }
    
    // handles page interactions
    private KMSelectable.OnInteractHandler PagePress(int i) {
        return delegate {
            pageButtons[i].AddInteractionPunch(.1f);

            if (gameState == 9 || animateState) {
                return false;
            }
            if (i == 1 && gameState < 6) {
                gameState = currentLetterPage;
                StartCoroutine(AnimateToLetters());
            }
            else if (i == 1 && gameState >= 6) {
                gameState = currentBoard;
                StartCoroutine(AnimateToBoards());
            }
            else if (gameState >= 6) {
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, pageButtons[i].transform);
                currentLetterPage = (currentLetterPage + i - 1) % 3 + 6;
                gameState = currentLetterPage;
                UpdateBoard();
            }
            else {
                Audio.PlaySoundAtTransform(NextAudio(), Module.transform);
                currentBoard = (currentBoard + i - 1) % 6;
                currentBoard += (currentBoard == -1 ? 6 : 0);
                gameState = currentBoard;
                UpdateBoard();
            }
            return false;
        };
    }

    private void UpdateBoard() {
        if (gameState < 6) {
            for (int i = 0; i < 9; i++) {
                SetGridInfo(i, answerBoards[gameState].GetInitial(i), 
                    answerBoards[gameState].GetHighlight() == i ? new Color32(214, 209, 49, 255) : new Color32(247, 135, 2, 255));
            }
        }
        else if (gameState >= 6 && gameState < 9) {
            int textLength = inputText.Length;
            for (int i = 0; i < 9; i++) {
                Color color;
                if (gameState == 8 && i == 8) color = Color.red;
                else if (textLength > 0 && alphabet[(gameState - 6) * 9 + i] == currentHighlightedLetter) color = new Color32(214, 209, 49, 255);
                else color = new Color32(247, 135, 2, 255);
                SetGridInfo(i, alphabet[(gameState - 6) * 9 + i], color);
            }
        }
    }
    
    private IEnumerator AnimateToBoards() {
        animateState = true;

        for (int i = 0; i < 9; i++) {
            Audio.PlaySoundAtTransform("animationTick", Module.transform);
            SetGridInfo(i, answerBoards[gameState].GetInitial(i), answerBoards[gameState].GetHighlight() == i ? new Color32(214, 209, 49, 255) : new Color32(247, 135, 2, 255));
            yield return new WaitForSeconds(animateDelay);
        }

        animateState = false;
        yield return null;
    }
    
    private IEnumerator AnimateToLetters() {
        animateState = true;
        int textLength = inputText.Length;
        
        for (int i = 0; i < 9; i++) {
            Audio.PlaySoundAtTransform("animationTick", Module.transform);
            Color color;
            if (gameState == 8 && i == 8) color = Color.red;
            else if (textLength > 0 && alphabet[(gameState - 6) * 9 + i] == currentHighlightedLetter) color = new Color32(214, 209, 49, 255);
            else color = new Color32(247, 135, 2, 255);
            SetGridInfo(i, alphabet[(gameState - 6) * 9 + i], color);
            yield return new WaitForSeconds(animateDelay);
        }

        animateState = false;
        yield return null;
    }

    private IEnumerator AnimateWords(List<string> list) {
        animateState = true;
        Color colorBack = gameState == 9 ? Color.green : Color.red;
        for (int i = 0; i < 3; i++) {
            Audio.PlaySoundAtTransform("animationTick", Module.transform);
            for (int j = 0; j < 3; j++) {
                SetGridInfo(i * 3 + j, list[i][j], colorBack);
            }
            yield return new WaitForSeconds(submitDelay);
        }
        if (gameState == 9) {
            Audio.PlaySoundAtTransform("pagePressA3", Module.transform);
            inputTextMesh.color = Color.green;
            Module.HandlePass(); 
        }
        else {
            inputTextMesh.color = Color.red;
            Module.HandleStrike();
            yield return new WaitForSeconds(submitDelay);
            ModifyInputText("");
            inputTextMesh.color = Color.white;
            currentBoard = UnityEngine.Random.Range(0, 6);
            currentLetterPage = 6;
            gameState = currentBoard;
            StartCoroutine(AnimateToBoards());
        }
        
        animateState = false;
        yield return null;
    }

    private void SetGridInfo(int pos, char display, Color color) {
        gridTextMeshes[pos].text = display.ToString();
        gridColors[pos].material.color = color;
    }

    private String NextAudio() {
        audioPointer = (audioPointer + 1) % 8;
        return audioNames[audioPointer];
    }

    private void ModifyInputText(string newText) {
        inputText = newText;
        inputTextMesh.text = inputText + "      ".Substring(0, 6 - inputText.Length);
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use <!{0} up/down/toggle> to use the page buttons. Use <!{0} a1/tl/top-left/topleft/1> to press the top left button in the grid.
        Commands can be chained with spaces.";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand (string command) {
        List<string> pageNames = new List<string>() {"UP", "TOGGLE", "DOWN", "U", "T", "D"},
            boardNames = new List<string>() {
                "A1", "B1", "C1", "A2", "B2", "C2", "A3", "B3", "C3",
                "TL", "TM", "TR", "ML", "MM", "MR", "BL", "BM", "BR",
                "TOP-LEFT", "TOP-MIDDLE", "TOP-RIGHT", "MIDDLE-LEFT", "MIDDLE-MIDDLE", "MIDDLE-RIGHT", "BOTTOM-LEFT", "BOTTOM-MIDDLE", "BOTTOM-RIGHT",
                "TOPLEFT", "TOPMIDDLE", "TOPRIGHT", "MIDDLELEFT", "MIDDLEMIDDLE", "MIDDLERIGHT", "BOTTOMLEFT", "BOTTOMMIDDLE", "BOTTOMRIGHT",
                "1", "2", "3", "4", "5", "6", "7", "8", "9"
            };
        
        command = command.Trim().ToUpperInvariant();
        List<string> parameters = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parameters.Any(s => !pageNames.Contains(s) && !boardNames.Contains(s))) {
            yield return "sendtochaterror Invalid command: " + parameters.First(s => !pageNames.Contains(s) && !boardNames.Contains(s));
        }
        else {
            yield return null;
            foreach (string s in parameters) {
                if (pageNames.Contains(s)) {
                    pageButtons[pageNames.IndexOf(s) % 3].OnInteract();
                    yield return new WaitForSeconds(0.1f + (pageNames.IndexOf(s) % 3 == 1 ? animateDelay * 9 : 0));
                }
                else {
                    boardButtons[boardNames.IndexOf(s) % 9].OnInteract();
                    if (inputText.Length == 6) {
                        yield return (inputText == answer ? "solve" : "strike");
                        break;
                    }
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }
        
        yield return null;
    }

    IEnumerator TwitchHandleForcedSolve ()
    {
        yield return null;
    }
}

public class BoardSet {
    private string initial, solved;
    private int highlight;

    public BoardSet(string initial, string solved) {
        this.initial = initial;
        this.solved = solved;
        highlight = -1;
    }
    
    public char GetInitial(int pos) {
        return initial[pos];
    }

    public int GetHighlight() {
        return highlight;
    }

    public void SetHighlight(int pos) {
        highlight = pos;
    }

    public bool IsValid(int space, char goal) {
        return initial[space] == ' ' && solved[space] == goal;
    }
}
