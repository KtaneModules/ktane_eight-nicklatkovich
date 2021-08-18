using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class EightModule : MonoBehaviour {
	private const int DIGITS_COUNT = 8;
	private const float DIGITS_HEIGHT = 0.021f;
	private const float DIGITS_INTERVAL = 0.014f;

	private static int _moduleIdCounter = 1;

	public static readonly string[] addendum = new string[DIGITS_COUNT] {
		"4280752097",
		"8126837692",
		"5317800685",
		"9852322448",
		"3710561298",
		"6154187606",
		"8863108821",
		"4628679367",
	};

	public readonly string TwitchHelpMessage = new string[] {
		"\"!{0} submit 123\" or \"!{0} remove 123\" to submit/remove digits by its indices",
		"\"!{0} skip\" or \"!{0} submit\" to press button with label SKIP",
	}.Join(" | ");

	public GameObject Display;
	public KMAudio Audio;
	public KMBombInfo BombInfo;
	public KMBombModule BombModule;
	public KMSelectable SkipButton;
	public Character Stage;
	public SelectableDigit SelectableDigitPrefab;

	private bool _forceSolved = false;
	public bool forceSolved { get { return _forceSolved; } }

	private int _souvenirLastStageDigit;
	public int souvenirLastStageDigit { get { return _souvenirLastStageDigit; } }

	private int _souvenirLastBrokenDigitPosition;
	public int souvenirLastBrokenDigitPosition { get { return _souvenirLastBrokenDigitPosition; } }

	private int _souvenirLastResultingDigits;
	public int souvenirLastResultingDigits { get { return _souvenirLastResultingDigits; } }

	private int _souvenirLastDisplayedNumber = -1;
	public int souvenirLastDisplayedNumber { get { return _souvenirLastDisplayedNumber; } }

	public HashSet<int> souvenirPossibleLastResultingDigits {
		get {
			int limit = 20;
			HashSet<int> result = new HashSet<int>();
			while (limit-- > 0) {
				if (result.Count > 5) return result;
				result.Add(GenerateLastStageNumber());
			}
			return result;
		}
	}

	public HashSet<int> souvenirPossibleLastNumbers {
		get {
			int limit = 20;
			HashSet<int> result = new HashSet<int>();
			while (limit-- > 0) {
				if (result.Count > 5) return result;
				result.Add(GenerateLastStageNumber());
			}
			return result;
		}
	}

	private bool solved = false;
	private int solvesCount;
	private int remainingMinutes;
	private int moduleId;
	private int[] values = new int[DIGITS_COUNT];
	private readonly SelectableDigit[] digits = new SelectableDigit[DIGITS_COUNT];
	private HashSet<int> notDisabledDigits = new HashSet<int>(Enumerable.Range(0, DIGITS_COUNT));

	private void Start() {
		moduleId = _moduleIdCounter++;
		KMSelectable selfSelectable = GetComponent<KMSelectable>();
		selfSelectable.Children = new KMSelectable[DIGITS_COUNT + 1];
		for (int i = 0; i < DIGITS_COUNT; i++) {
			SelectableDigit digit = Instantiate(SelectableDigitPrefab);
			digit.transform.parent = Display.transform;
			float x = DIGITS_INTERVAL * (i - (DIGITS_COUNT - 1) / 2f);
			digit.transform.localPosition = new Vector3(x, DIGITS_HEIGHT, 0f);
			digit.transform.localRotation = new Quaternion();
			digit.transform.localScale = Vector3.one;
			digit.Actualized += () => OnDigitActualized();
			KMSelectable digitSelectable = digit.GetComponent<KMSelectable>();
			digitSelectable.Parent = selfSelectable;
			selfSelectable.Children[i] = digitSelectable;
			int digitIndex = i;
			digitSelectable.OnInteract += () => OnDigitPressed(digitIndex);
			digits[i] = digit;
		}
		selfSelectable.Children[DIGITS_COUNT] = SkipButton;
		selfSelectable.UpdateChildren();
		SkipButton.OnInteract += () => OnSkipPressed();
		GetComponent<KMBombModule>().OnActivate += () => Activate();
	}

	private void Activate() {
		foreach (var digit in digits) digit.character = '0';
		remainingMinutes = GetRemainingMinutes();
		solvesCount = GetSolvesCount();
		GenerateDigits();
		StartCoroutine(CustomUpdate());
	}

	private IEnumerator<object> CustomUpdate() {
		while (!solved) {
			int newRemainingMinutes = GetRemainingMinutes();
			if (newRemainingMinutes != remainingMinutes) {
				remainingMinutes = newRemainingMinutes;
				UpdateDigit(6);
			}
			int newSolvesCount = GetSolvesCount();
			if (newSolvesCount != solvesCount) {
				solvesCount = newSolvesCount;
				UpdateDigit(3);
			}
			yield return new WaitForSeconds(.1f);
		}
	}

	public IEnumerator ProcessTwitchCommand(string command) {
		command = command.Trim().ToLower();
		if (command == "skip" || command == "submit") {
			yield return null;
			yield return new KMSelectable[] { SkipButton };
			yield break;
		}
		if (Regex.IsMatch(command, @"^submit +([1-8] ?)+$")) {
			string[] split = command.Split(' ');
			HashSet<int> indices = new HashSet<int>(command.Split(' ').Skip(1).Where(s => s.Length > 0).Join("").ToCharArray().Select((c) => c - '0' - 1));
			if (indices.Any((i) => digits[i].disabled || digits[i].removed)) yield break;
			int[] indicesToRemove = Enumerable.Range(0, DIGITS_COUNT).Where((i) => (
				!indices.Contains(i)
			)).ToArray();
			yield return null;
			yield return indicesToRemove.Select((i) => digits[i].GetComponent<KMSelectable>()).ToList().Concat(
				new KMSelectable[] { SkipButton }
			).ToArray();
			if (solved) yield return "solve";
			yield break;
		}
		if (Regex.IsMatch(command, @"^remove +([1-8] ?)+$")) {
			int[] indices = command.Split(' ').Skip(1).Where(s => s.Length > 0).Join("").ToCharArray().Select((c) => c - '0' - 1).ToArray();
			yield return null;
			yield return indices.Where((i) => (
				!digits[i].disabled && !digits[i].removed
			)).Select((i) => digits[i].GetComponent<KMSelectable>()).Cast<KMSelectable>().ToArray();
			yield break;
		}
	}

	public void TwitchHandleForcedSolve() {
		Debug.LogFormat("[Eight #{0}] Force-solved", moduleId);
		_forceSolved = true;
		Solve();
	}

	private void OnDigitActualized() {
		if (!solved) return;
		if (digits.Any((d) => !d.actual)) return;
		BombModule.HandlePass();
	}

	private bool OnDigitPressed(int index) {
		if (solved) return false;
		var digit = digits[index];
		if (!digit.active || digit.removed || digit.disabled) return false;
		Audio.PlaySoundAtTransform("DigitPressed", digits[index].transform);
		Debug.LogFormat("[Eight #{0}] Digit #{1} removed", moduleId, index + 1);
		digits[index].removed = true;
		return false;
	}

	private bool OnSkipPressed() {
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);
		if (solved) return false;
		Debug.LogFormat("[Eight #{0}] \"SKIP\" button pressed", moduleId);
		var possibleSolution = GetPossibleSolution();
		if (possibleSolution == null ? OnCorrectAnswer() : ValidateAnswer()) return false;
		foreach (var digit in digits) digit.removed = false;
		GenerateDigits();
		return false;
	}

	private bool ValidateAnswer() {
		var resultString = values.Where((_, i) => {
			var digit = digits[i];
			return !digit.removed && !digit.disabled;
		}).Select((v) => v.ToString()).Join("");
		if (resultString.Length == 0) {
			Debug.LogFormat("[Eight #{0}] All digits has been removed", moduleId);
			Strike();
			return false;
		}
		if (resultString.StartsWith("0")) {
			Debug.LogFormat("[Eight #{0}] Submitted number has leading 0", moduleId);
			Strike();
			return false;
		}
		var resultNumber = int.Parse(resultString);
		if (resultNumber % 8 == 0) return OnCorrectAnswer();
		Debug.LogFormat("[Eight #{0}] Submitted number {1} not divisible by 8", moduleId, resultNumber);
		Strike();
		return false;
	}

	private void Strike() {
		BombModule.HandleStrike();
		_souvenirLastDisplayedNumber = -1;
		foreach (SelectableDigit digit in digits) digit.disabled = false;
		notDisabledDigits = new HashSet<int>(Enumerable.Range(0, DIGITS_COUNT));
	}

	private int? GetPossibleSolution() {
		int[] availableDigits = digits.Where((d) => d.active && !d.disabled).Select((d) => d.value).ToArray();
		for (int i = 0; i < availableDigits.Length; i++) {
			int v1 = availableDigits[i];
			if (v1 == 8) return v1;
			for (int j = i + 1; j < availableDigits.Length; j++) {
				int v2 = v1 * 10 + availableDigits[j];
				if (v2 != 0 && v2 % 8 == 0) return v2;
				for (int k = j + 1; k < availableDigits.Length; k++) {
					int v3 = v2 * 10 + availableDigits[k];
					if (v3 != 0 && v3 % 8 == 0) return v3;
				}
			}
		}
		return null;
	}

	private bool Solve() {
		solved = true;
		foreach (var digit in digits) {
			digit.disabled = false;
			digit.removed = false;
			digit.character = '8';
			digit.active = false;
		}
		Stage.character = '8';
		return true;
	}

	private bool OnCorrectAnswer() {
		if (notDisabledDigits.Count == 2) return Solve();
		int digitToDisable = notDisabledDigits.PickRandom();
		_souvenirLastBrokenDigitPosition = digitToDisable;
		Debug.LogFormat("[Eight #{0}] Digit #{1} disabled", moduleId, digitToDisable + 1);
		notDisabledDigits.Remove(digitToDisable);
		digits[digitToDisable].disabled = true;
		return false;
	}

	private void GenerateDigits() {
		int stage = Random.Range(0, 10);
		Stage.character = (char)('0' + stage);
		_souvenirLastStageDigit = stage;
		Debug.LogFormat("[Eight #{0}] New digit on small display: {1}", moduleId, Stage.character);
		if (notDisabledDigits.Count == 2) {
			GenerateTwoDigits();
			return;
		}
		if (notDisabledDigits.Count == 3) {
			GenerateThreeDigits();
			return;
		}
		var possibleDigits = new HashSet<int>(Enumerable.Range(0, 9));
		possibleDigits.Remove(8);
		int lastDigit = Random.Range(0, 3) * 2;
		switch (lastDigit) {
			case 0: possibleDigits.Remove(4); break;
			case 2: foreach (int i in new int[] { 3, 7 }) possibleDigits.Remove(i); break;
			case 4: foreach (int i in new int[] { 2, 6 }) possibleDigits.Remove(i); break;
			case 6: foreach (int i in new int[] { 1, 5, 9 }) possibleDigits.Remove(i); break;
			default: throw new UnityException("Unexpected last digit");
		}
		for (var i = 0; i < DIGITS_COUNT - 1; i++) {
			if (digits[i].disabled) continue;
			int digit = possibleDigits.PickRandom();
			switch (digit) {
				case 1:
				case 5:
				case 9: possibleDigits.Remove(6); break;
				case 2:
				case 6: possibleDigits.Remove(4); break;
				case 3:
				case 7: possibleDigits.Remove(2); break;
				case 4: possibleDigits.Remove(0); break;
			}
			values[i] = digit;
		}
		values[Enumerable.Range(0, DIGITS_COUNT).Where((i) => !digits[i].disabled).Max()] = lastDigit;
		UpdateDigits();
	}

	private void GenerateThreeDigits() {
		for (var i = 0; i < DIGITS_COUNT; i++) {
			if (digits[i].disabled) continue;
			values[i] = Random.Range(0, 10);
		}
		UpdateDigits();
	}

	private int GenerateLastStageNumber() {
		switch (Random.Range(0, 3)) {
			case 0: return Random.Range(0, 2) == 0 ? Random.Range(2, 13) * 8 : Random.Range(0, 100);
			case 1: return 80 + Random.Range(0, 5) * 2;
			default: return Random.Range(0, 10) * 10 + 8;
		}
	}

	private void GenerateTwoDigits() {
		int v = GenerateLastStageNumber();
		_souvenirLastResultingDigits = v;
		bool remainingDigitsIsStatic = true;
		int firstNotDisabledDigitIndex = -1;
		int secondNotDisabledDigitIndex = -1;
		for (var i = DIGITS_COUNT - 1; i >= 0; i--) {
			if (digits[i].disabled) continue;
			values[i] = v % 10;
			v /= 10;
			if (!IsDigitStatic(i)) remainingDigitsIsStatic = false;
			if (secondNotDisabledDigitIndex == -1) secondNotDisabledDigitIndex = i;
			else firstNotDisabledDigitIndex = i;
		}
		if (remainingDigitsIsStatic) _souvenirLastDisplayedNumber = GetDigitAt(firstNotDisabledDigitIndex) * 10 + GetDigitAt(secondNotDisabledDigitIndex);
		UpdateDigits();
	}

	private void UpdateDigits() {
		for (var i = 0; i < DIGITS_COUNT; i++) UpdateDigit(i);
		Debug.LogFormat("[Eight #{0}] Generated number: {1}", moduleId, Enumerable.Range(0, DIGITS_COUNT).Where((i) => (
			!digits[i].disabled
		)).Select((i) => values[i]).Join(""));
		int? possibleSolution = GetPossibleSolution();
		if (possibleSolution == null) Debug.LogFormat("[Eight #{0}] No possible solution", moduleId, possibleSolution);
		else Debug.LogFormat("[Eight #{0}] Possible solution: {1}", moduleId, possibleSolution);
		Debug.LogFormat("[Eight #{0}] New rendered number: {1}", moduleId, digits.Where((d) => (
			!d.disabled
		)).Select((d) => d.character).Join(""));
	}

	private void UpdateDigit(int digitIndex, bool log = false) {
		SelectableDigit digit = digits[digitIndex];
		if (digit.disabled) return;
		digit.value = values[digitIndex];
		int value = GetDigitAt(digitIndex);
		digit.character = (char)(value + '0');
		if (log) Debug.LogFormat("[Eight #{0}] Digit #{1} new rendered value: {2}", moduleId, digitIndex + 1, value);
	}

	private int GetDigitAt(int digitIndex) {
		int value = (values[digitIndex] - GetAddendum(digitIndex)) % 10;
		if (value < 0) value = 10 + value;
		return value;
	}

	private int GetAddendum(int digitIndex) {
		return GetBombAddendum(digitIndex) + addendum[digitIndex][(Stage.character ?? '0') - '0'] - '0';
	}

	private int GetBombAddendum(int digitIndex) {
		switch (digitIndex) {
			case 0: return BombInfo.GetIndicators().Count();
			case 1: return 8;
			case 2: return BombInfo.GetModuleIDs().Count;
			case 3: return solvesCount;
			case 4: return BombInfo.GetBatteryCount();
			case 5: return BombInfo.GetSerialNumberNumbers().Sum();
			case 6: return remainingMinutes;
			case 7: return BombInfo.GetPortCount();
			default: throw new UnityException("Invalid digit index");
		}
	}

	private bool IsDigitStatic(int digitIndex) {
		return digitIndex != 3 && digitIndex != 6;
	}

	private int GetRemainingMinutes() {
		return Mathf.FloorToInt(BombInfo.GetTime() / 60);
	}

	private int GetSolvesCount() {
		return BombInfo.GetSolvedModuleIDs().Count;
	}
}
