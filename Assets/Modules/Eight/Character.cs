using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour {
	public const char NOT_A_CHARACTER = '\0';

	public delegate void OnActualizedHandler();
	public event OnActualizedHandler Actualized;

	public TextMesh textMesh;
	public char minCharacter;
	public char maxCharacter;

	private bool _updating = false;
	public bool updating {
		get { return _updating; }
	}

	private char _activeCharacter;
	public char activeCharacter {
		get { return _activeCharacter; }
		private set {
			if (_activeCharacter == value) return;
			_activeCharacter = value;
			if (_activeCharacter == _character && Actualized != null) Actualized.Invoke();
			textMesh.text = _activeCharacter.ToString();
			UpdateMeshColor();
		}
	}

	private char _character;
	public char character {
		get { return _character; }
		set {
			if (_character == value) return;
			_character = value;
			if (!updating) StartCoroutine(UpdateCharacter());
		}
	}

	public bool actual {
		get { return character == activeCharacter; }
	}

	public char GetRandomCharacter() {
		return (char)Random.Range(minCharacter, maxCharacter + 1);
	}

	public virtual Color GetMeshColor() {
		return _activeCharacter != character ? Color.blue : Color.white;
	}

	protected virtual void Start() {
		activeCharacter = GetRandomCharacter();
		_character = minCharacter;
		character = NOT_A_CHARACTER;
	}

	private IEnumerator<object> UpdateCharacter() {
		_updating = true;
		yield return new WaitForSeconds(Random.Range(0f, 0.1f));
		while (true) {
			activeCharacter = (char)(activeCharacter + 1);
			if (activeCharacter > maxCharacter) activeCharacter = minCharacter;
			if (_character == activeCharacter) break;
			yield return new WaitForSeconds(.1f);
		}
		_updating = false;
	}

	protected void UpdateMeshColor() {
		textMesh.color = GetMeshColor();
	}
}
