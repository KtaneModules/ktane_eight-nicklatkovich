using System.Text.RegularExpressions;
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

	public readonly string TwitchHelpMessage =
		"`!{0} 3` - press digit by its position | `!{0} skip` - press button with label \"SKIP\"";

	public GameObject Display;
	public KMAudio Audio;
	public KMBombInfo BombInfo;
	public KMBombModule BombModule;
	public KMSelectable SkipButton;
	public Character Stage;
	public SelectableDigit SelectableDigitPrefab;

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
		GenerateDigits();
	}

	public KMSelectable[] ProcessTwitchCommand(string command) {
		command = command.Trim().ToLower();
		Debug.Log(command);
		if (Regex.IsMatch(command, @"[1-8]")) {
			return new KMSelectable[] { digits[int.Parse(command) - 1].GetComponent<KMSelectable>() };
		}
		if (command == "skip") return new KMSelectable[] { SkipButton };
		return null;
	}

	private void OnDigitActualized() {
	}

	private bool OnDigitPressed(int index) {
		var digit = digits[index];
		if (!digit.active || digit.removed || digit.disabled) return false;
		Debug.LogFormat("[Eight #{0}] Digit #{1} removed", moduleId, index);
		digits[index].removed = true;
		return false;
	}

	private bool OnSkipPressed() {
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);
		Debug.LogFormat("[Eight #{0}] \"SKIP\" button pressed", moduleId);
		var possibleSolution = GetPossibleSolution();
		if (possibleSolution == null ? OnCorrectAnswer() : ValidateAnswer(possibleSolution ?? 0)) return false;
		foreach (var digit in digits) digit.removed = false;
		GenerateDigits();
		return false;
	}

	private bool ValidateAnswer(int possibleSolution) {
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
		Debug.LogFormat("[Eight #{0}] Possible solution: {1}", moduleId, possibleSolution);
		Strike();
		return false;
	}

	private void Strike() {
		BombModule.HandleStrike();
		foreach (SelectableDigit digit in digits) digit.disabled = false;
		notDisabledDigits = new HashSet<int>(Enumerable.Range(0, DIGITS_COUNT));
	}

	private int? GetPossibleSolution() {
		int[] availableDigits = digits.Where((d) => d.active && !d.disabled).Select((d) => d.value).ToArray();
		for (var k = 2; k < availableDigits.Length; k++) {
			for (var j = 1; j < k; j++) {
				for (var i = 0; i < j; i++) {
					int firstDigit = availableDigits[i];
					if (firstDigit == 0) continue;
					int v = firstDigit * 100 + availableDigits[j] * 10 + availableDigits[k];
					if (v % 8 == 0) return v;
				}
			}
		}
		return null;
	}

	private bool OnCorrectAnswer() {
		if (notDisabledDigits.Count == 2) {
			BombModule.HandlePass();
			foreach (var digit in digits) {
				digit.disabled = false;
				digit.removed = false;
				digit.character = '8';
				digit.active = false;
			}
			return true;
		}
		int digitToDisable = notDisabledDigits.PickRandom();
		Debug.LogFormat("[Eight #{0}] Digit #{1} disabled", moduleId, digitToDisable);
		notDisabledDigits.Remove(digitToDisable);
		digits[digitToDisable].disabled = true;
		return false;
	}

	private void GenerateDigits() {
		if (notDisabledDigits.Count == 2) {
			GenerateTwoDigits();
			return;
		}
		var possibleDigits = new HashSet<int>(Enumerable.Range(0, 9));
		possibleDigits.Remove(8);
		var lastDigit = Random.Range(0, 3) * 2;
		switch (lastDigit) {
			case 0: possibleDigits.Remove(4); break;
			case 2: new int[] { 3, 7 }.Select((i) => possibleDigits.Remove(i)); break;
			case 4: new int[] { 2, 6 }.Select((i) => possibleDigits.Remove(i)); break;
			case 6: new int[] { 1, 5, 9 }.Select((i) => possibleDigits.Remove(i)); break;
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

	private void GenerateTwoDigits() {
		var v = 0;
		switch (Random.Range(0, 3)) {
			case 0: v = Random.Range(0, 2) == 0 ? Random.Range(2, 13) * 8 : Random.Range(0, 100); break;
			case 1: v = 80 + Random.Range(0, 5) * 2; break;
			case 2: v = Random.Range(0, 10) * 10 + 8; break;
		}
		for (var i = 0; i < DIGITS_COUNT; i++) {
			if (digits[i].disabled) continue;
			values[i] = v % 10;
			v /= 10;
		}
		UpdateDigits();
	}

	private void UpdateDigits() {
		var allDigits = "";
		for (var i = 0; i < DIGITS_COUNT; i++) {
			var digit = digits[i];
			if (digit.disabled) continue;
			digit.value = values[i];
			allDigits += digit.value.ToString();
			digit.character = (char)(values[i] + '0');
		}
		Debug.LogFormat("[Eight #{0}] Generated digits: {1}", moduleId, allDigits);
	}
}
