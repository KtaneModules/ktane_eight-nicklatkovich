using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class EightModule : MonoBehaviour {
	private const int LETTERS_COUNT = 8;
	private const float LETTER_HEIGHT = 0.021f;
	private const float LETTERS_INTERVAL = 0.014f;

	private static int _moduleIdCounter = 1;

	public static readonly string[] addendum = new string[LETTERS_COUNT] {
		"LWHTJNFSZO",
		"NFKMUIGVHD",
		"MGIJVFEYSW",
		"CQLYPZUTDX",
		"DTZSBGHFPU",
		"EBRGCHWJNV",
		"GIABZPMQKH",
		"OLSZGUNHRP",
	};

	public GameObject display;
	public KMAudio kMAudio;
	public KMBombInfo bombInfo;
	public KMSelectable skipButton;
	public Character stage;
	public Letter letterPrefab;

	public readonly string TwitchHelpMessage =
		"`!{0} 3` - press letter by its position | `!{0} skip` - press button with label \"SKIP\"";

	public readonly Letter[] letters = new Letter[LETTERS_COUNT];
	public bool shouldPassOnActivation;
	public int skipsToAnswer;
	public int startingTime;
	public Queue<int> expectedAnswers = new Queue<int>();
	public List<int> nextExpectedAnswers;

	private bool _active = true;
	public bool active {
		get { return _active; }
		set {
			if (_active == value) return;
			_active = value;
			if (!value) {
				foreach (Letter letter in letters) letter.active = value;
			}
		}
	}

	private bool _solved = false;
	public bool solved {
		get { return _solved; }
	}

	private int _moduleId;
	public int moduleId {
		get { return _moduleId; }
	}

	// Souvenir data
	private char _pressedLetter;
	public char pressedLetter {
		get { return _pressedLetter; }
	}

	private char _letterToTheLeftOfPressedOne;
	public char letterToTheLeftOfPressedOne {
		get { return _letterToTheLeftOfPressedOne; }
	}

	private char _letterToTheRightOfPressedOne;
	public char letterToTheRightOfPressedOne {
		get { return _letterToTheRightOfPressedOne; }
	}

	private int _displayedDigit;
	public int displayedDigit {
		get { return _displayedDigit; }
	}

	private void Start() {
		_moduleId = _moduleIdCounter++;
		nextExpectedAnswers = new int[LETTERS_COUNT - 2].Select((_, i) => i + 1).ToList();
		skipsToAnswer = Random.Range(0, 4);
		KMSelectable selfSelectable = GetComponent<KMSelectable>();
		selfSelectable.Children = new KMSelectable[LETTERS_COUNT + 1];
		for (int i = 0; i < LETTERS_COUNT; i++) {
			Letter letter = Instantiate(letterPrefab);
			letter.transform.parent = display.transform;
			float x = LETTERS_INTERVAL * (i - (LETTERS_COUNT - 1) / 2f);
			letter.transform.localPosition = new Vector3(x, LETTER_HEIGHT, 0f);
			letter.transform.localRotation = new Quaternion();
			letter.Actualized += () => OnLetterActualized();
			KMSelectable letterSelectable = letter.GetComponent<KMSelectable>();
			letterSelectable.Parent = selfSelectable;
			selfSelectable.Children[i] = letterSelectable;
			int letterIndex = i;
			letterSelectable.OnInteract += () => {
				PressLetter(letterIndex);
				return false;
			};
			letters[i] = letter;
		}
		selfSelectable.Children[LETTERS_COUNT] = skipButton;
		selfSelectable.UpdateChildren();
		skipButton.OnInteract += () => {
			if (active) {
				Debug.LogFormat("[Eight #{0}] \"SKIP\" button pressed", moduleId);
				kMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);
				RandomizeLetters();
			}
			return false;
		};
		GetComponent<KMBombModule>().OnActivate += () => Activate();
	}

	private void Activate() {
		startingTime = GetRemaingMinutes();
		RandomizeLetters();
	}

	public void PressLetter(int index) {
		if (!stage.actual) return;
		bool? correct = ButtonIsCorrect(index);
		if (correct == null) return;
		_pressedLetter = letters[index].character;
		_letterToTheLeftOfPressedOne = letters[index - 1].character;
		_letterToTheRightOfPressedOne = letters[index + 1].character;
		_displayedDigit = stage.character - '0';
		shouldPassOnActivation = (bool)correct;
		active = false;
		letters[0].character = Character.NOT_A_CHARACTER;
		letters[LETTERS_COUNT - 1].character = Character.NOT_A_CHARACTER;
		stage.character = Character.NOT_A_CHARACTER;
		string message = shouldPassOnActivation ? "SOLVED" : "STRIKE";
		for (int i = 0; i < 6; i++) letters[i + 1].character = message[i];
	}

	public void RandomizeLetters() {
		stage.character = stage.GetRandomCharacter();
		Debug.LogFormat("[Eight #{0}] New digit on small display is {1}", moduleId, stage.character);
		foreach (Letter letter in letters) {
			letter.active = true;
			letter.character = letter.GetRandomCharacter();
		}
		if (skipsToAnswer <= 0) {
			skipsToAnswer = Random.Range(0, 4);
			if (expectedAnswers.Count == 0) {
				expectedAnswers = new Queue<int>(nextExpectedAnswers.Shuffle());
				nextExpectedAnswers = new List<int>();
			}
			int candidateIndex = expectedAnswers.Dequeue();
			nextExpectedAnswers.Add(candidateIndex);
			char[] ab = Random.Range(0, 2) == 0 ? new char[] { 'A', 'B' } : new char[] { 'B', 'A' };
			SetLetterToExpected(candidateIndex, ab[0]);
			SetLetterToExpected(candidateIndex - 1, ab[1]);
			SetLetterToExpected(candidateIndex + 1, ab[1]);
		} else skipsToAnswer -= 1;
	}

	public KMSelectable[] ProcessTwitchCommand(string command) {
		command = command.Trim().ToLower();
		Debug.Log(command);
		if (Regex.IsMatch(command, @"[1-8]")) {
			return new KMSelectable[] { letters[int.Parse(command) - 1].GetComponent<KMSelectable>() };
		}
		if (command == "skip") return new KMSelectable[] { skipButton };
		return null;
	}

	public int GetLetterAddendum(int index) {
		char letterAddendum = addendum[index][stage.character - '0'];
		int valueAddendum = GetValueAddendum(index);
		int result = letterAddendum - 'A' + valueAddendum;
		Debug.LogFormat("[Eight #{0}] Current addendum to letter #{1} is {2} ({3}+'{4}')", moduleId, index + 1,
			result, valueAddendum, letterAddendum);
		return result;
	}

	public int GetValueAddendum(int index) {
		switch (index) {
			case 0: return bombInfo.GetPortCount();
			case 1: return startingTime;
			case 2: return GetRemaingMinutes();
			case 3:
				if (bombInfo.IsTwoFactorPresent()) return bombInfo.GetTwoFactorCodes().Select((c) => c % 10).Sum();
				return bombInfo.GetSolvedModuleIDs().Count;
			case 4: return bombInfo.GetSerialNumberNumbers().Sum();
			case 5: return bombInfo.GetStrikes() + bombInfo.GetModuleIDs().Count;
			case 6: return bombInfo.GetBatteryCount();
			case 7: return bombInfo.GetIndicators().Count();
			default: throw new UnityException("Invalid index provided");
		}
	}

	public bool? ButtonIsCorrect(int index) {
		Letter letter = letters[index];
		if (!letter.actual) return null;
		if (index == 0 || index == LETTERS_COUNT - 1) return false;
		char pressedLetter = MakeMutations(index, letter.character);
		if (pressedLetter > 'B') return false;
		char expectedNearLetters = pressedLetter == 'A' ? 'B' : 'A';
		Letter leftLetter = letters[index - 1];
		if (!leftLetter.actual || MakeMutations(index - 1, leftLetter.character) != expectedNearLetters) return false;
		Letter rightLetter = letters[index + 1];
		if (!rightLetter.actual || MakeMutations(index + 1, rightLetter.character) != expectedNearLetters) return false;
		return true;
	}

	public char MakeMutations(int index, char character) {
		int addendum = GetLetterAddendum(index);
		return (char)((character - 'A' + addendum) % 26 + 'A');
	}

	private int GetRemaingMinutes() {
		return Mathf.FloorToInt(bombInfo.GetTime() / 60f);
	}

	private void OnLetterActualized() {
		if (active) return;
		for (int j = 0; j < 6; j++) {
			if (!letters[j + 1].actual) return;
		}
		KMBombModule selfBombModule = GetComponent<KMBombModule>();
		if (shouldPassOnActivation) {
			selfBombModule.HandlePass();
			_solved = true;
		} else {
			selfBombModule.HandleStrike();
			active = true;
		}
	}

	private void SetLetterToExpected(int index, char expected) {
		Debug.LogFormat("[Eight #{0}] Trying to set resulting letter #{1} to {2}", moduleId, index + 1, expected);
		int addendum = GetLetterAddendum(index);
		int newValue = expected - 'A' - addendum;
		if (index == 2) {
			int remaingMinutes = GetRemaingMinutes();
			if (remaingMinutes > 0) {
				int reserveTime = Random.Range(Mathf.Min(remaingMinutes, 2), Mathf.Min(6, remaingMinutes + 1));
				Debug.LogFormat("[Eight #{0}] Add {1} delay minutes to letter #3", moduleId, reserveTime);
				newValue += reserveTime;
			}
		}
		newValue %= 26;
		if (newValue < 0) newValue = 26 + newValue;
		char newCharacter = (char)(newValue + 'A');
		Debug.LogFormat("[Eight #{0}] Setting letter #{1} to '{2}'", moduleId, index + 1, newCharacter);
		letters[index].character = newCharacter;
	}
}
