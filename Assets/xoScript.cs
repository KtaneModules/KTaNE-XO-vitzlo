using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using NUnit.Framework;

public class xoScript: MonoBehaviour {

    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBombModule Module;

    public KMSelectable[] boardButtons, pageButtons;
    public MeshRenderer[] gridColors;
    public TextMesh inputTextMesh;
    public TextMesh[] gridTextMeshes;

    private const string alphabet = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string boardAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ🚫";
    private const string pieces = "X O";
    private readonly string[] loggingPieces = {"X mark", "empty space", "O mark"};
    private readonly string[] locations = {"top-left", "top-middle", "top-right", "middle-left", "center", "middle-right",
        "bottom-left", "bottom-middle", "bottom-right"};
    private string[] yesList = {"YEA", "YUP", "2EZ", "TKO", "WIN", "DUB"},
        noList = {"HUH", "HOW", "UHH", "404", "LOL", "RIP"};

    private string moduleKey, inputText, answer;
    private List<BoardSet> answerBoards = new List<BoardSet>();
    private List<List<BoardSet>> boardPool = new List<List<BoardSet>>(), backupBoardPool = new List<List<BoardSet>>();
    private List<string> wordBank;
    
    private int currentBoard, currentLetterPage;
    private int gameState; // 0-5: viewing board  6-8: submission pages 9: solved module 10: struck module
    private bool animateState = false;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake () {
        moduleId = moduleIdCounter++;
        ModifyInputText("");
    }

    void Start () {
        wordBank = new List<string>() {"AIMING", "ANIMAL", "ANSWER", "APATHY", "ASYLUM", "AVENUE",
            "BEHEAD", "BOTHER", "BROKEN", "BUCKET", "BUNDLE", "BURIAL", "CHURCH", "CORNER", "CRISIS", "CROUCH", 
            "DOCTOR", "DRAGON", "DRAWER", "DRIVER", "EMBRYO", "EMPLOY", "ENERGY", "EXEMPT", 
            "FAMILY", "FATHER", "FILTER", "FLOWER", "FORMAL", "FORMAT", "FROZEN", "GARLIC", "GRAVEL", "GROUND", "GUITAR",
            "HARBOR", "HEAVEN", "HUNTER", "INJURY", "INVITE", "ISLAND", "JOCKEY", "JUNGLE", "JUNIOR",
            "KEYPAD", "KIDNEY", "LAUNCH", "LINEAR", "MAKEUP", "MARBLE", "MARINE", "MARKET", "MEADOW", "MEMBER", "METHOD", "MISERY",
            "NATIVE", "NUMBER", "PATENT", "PERIOD", "PLANET", "PLEASE", "PUBLIC", "PUNISH", "QUAINT"
        }; // no two adjacent letters can have the same highlight
        InitializeBoardPool();
        
        for (int i = 0; i < 9; i++) boardButtons[i].OnInteract = BoardPress(i);
        for (int i = 0; i < 3; i++) pageButtons[i].OnInteract = PagePress(i);

        answer = wordBank[UnityEngine.Random.Range(0, wordBank.Count)];
        int snKey = (alphabet.IndexOf(Bomb.GetSerialNumber()[3]) - Bomb.GetSerialNumberNumbers().Sum() + 14) % 27;
        while (snKey < 0) {
            snKey += 27;
        }
        moduleKey = alphabet.Substring(snKey, 27 - snKey) + alphabet.Substring(0, snKey);
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
            new BoardSet(" XO O  X ", "OXXOO OXX"),
            new BoardSet("O  XOX   ", "OXOXOXO X"),
            new BoardSet(" X  O OX ", "OXXXO OXO")
        });
        boardPool.Add(new List<BoardSet>() {
            new BoardSet("O     XXO", "OOX X XXO"), // O.. > OOX
            new BoardSet("  O  XO X", "O OXXXO X"), // ... > .X.
            new BoardSet("OOX     O", "OXX X OXO"), // XXO > XXO and its variations
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
    }

    void Update () {

    }

    // handles interactions inside the board
    private KMSelectable.OnInteractHandler BoardPress(int i) {
        return delegate {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, boardButtons[i].transform);
            boardButtons[i].AddInteractionPunch(.1f);

            if (gameState == 9 || animateState) {
                return false;
            }
            if (gameState == 8 && i == 8) {
                ModifyInputText("");
                UpdateBoard();
            }
            else if (gameState >= 6) {
                ModifyInputText(inputText + alphabet[(gameState - 6) * 9 + i + 1]);
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
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, boardButtons[i].transform);
            boardButtons[i].AddInteractionPunch(.1f);

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
                currentLetterPage = (currentLetterPage + i - 1) % 3 + 6;
                gameState = currentLetterPage;
                UpdateBoard();
            }
            else {
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
                if (gameState == 8 && i == 8) {
                    color = Color.red;
                }
                else if (textLength > 0 && boardAlphabet.IndexOf(inputText[textLength - 1]) / 9 == gameState - 6 &&
                         boardAlphabet.IndexOf(inputText[textLength - 1]) % 9 == i) {
                    color = new Color32(214, 209, 49, 255);
                }
                else {
                    color = new Color32(247, 135, 2, 255);
                }
                SetGridInfo(i, boardAlphabet[(gameState - 6) * 9 + i], color);
            }
        }
    }
    
    private IEnumerator AnimateToBoards() {
        animateState = true;

        for (int i = 0; i < 9; i++) {
            SetGridInfo(i, answerBoards[gameState].GetInitial(i), answerBoards[gameState].GetHighlight() == i ? new Color32(214, 209, 49, 255) : new Color32(247, 135, 2, 255));
            yield return new WaitForSeconds(0.1f);
        }

        animateState = false;
        yield return null;
    }
    
    private IEnumerator AnimateToLetters() {
        animateState = true;
        int textLength = inputText.Length;
        
        for (int i = 0; i < 9; i++) {
            Color color;
            if (gameState == 8 && i == 8) {
                color = Color.red;
            }
            else if (textLength > 0 && boardAlphabet.IndexOf(inputText[textLength - 1]) / 9 == gameState - 6 &&
                     boardAlphabet.IndexOf(inputText[textLength - 1]) % 9 == i) {
                color = new Color32(214, 209, 49, 255);
            }
            else {
                color = new Color32(247, 135, 2, 255);
            }
            SetGridInfo(i, boardAlphabet[(gameState - 6) * 9 + i], color);
            yield return new WaitForSeconds(0.1f);
        }

        animateState = false;
        yield return null;
    }

    private IEnumerator AnimateWords(List<string> list) {
        animateState = true;
        Color colorBack = gameState == 9 ? Color.green : Color.red;
        for (int i = 0; i < 3; i++) {
            for (int j = 0; j < 3; j++) {
                SetGridInfo(i * 3 + j, list[i][j], colorBack);
            }
            yield return new WaitForSeconds(0.5f);
        }
        if (gameState == 9)
        {
            inputTextMesh.color = Color.green;
            Module.HandlePass(); 
        }
        else {
            inputTextMesh.color = Color.red;
            Module.HandleStrike();
            yield return new WaitForSeconds(0.5f);
            ModifyInputText("");
            inputTextMesh.color = Color.black;
            gameState = UnityEngine.Random.Range(0, 6);
            StartCoroutine(AnimateToBoards());
        }
        
        animateState = false;
        yield return null;
    }

    private void SetGridInfo(int pos, char display, Color color) {
        gridTextMeshes[pos].text = display.ToString();
        gridColors[pos].material.color = color;
    }

    private void ModifyInputText(string newText) {
        inputText = newText;
        inputTextMesh.text = newText + "      ".Substring(0, 6 - newText.Length);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use <!{0} foobar> to do something.";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand (string command)
    {
        command = command.Trim().ToUpperInvariant();
        List<string> parameters = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
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
