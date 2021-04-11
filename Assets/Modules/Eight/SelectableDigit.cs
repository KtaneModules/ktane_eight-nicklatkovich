using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectableDigit : Character {
	public int value;

	private bool _highlighted = false;
	public bool highlighted {
		get { return _highlighted; }
		private set {
			if (_highlighted == value) return;
			_highlighted = value;
			UpdateMeshColor();
		}
	}

	private bool _active = true;
	public bool active {
		get { return _active; }
		set {
			if (_active == value) return;
			_active = value;
			UpdateMeshColor();
		}
	}

	private bool _removed = false;
	public bool removed {
		get { return _removed; }
		set {
			if (_removed == value) return;
			_removed = value;
			UpdateMeshColor();
		}
	}

	private bool _disabled = false;
	public bool disabled {
		get { return _disabled; }
		set {
			_disabled = value;
			if (value) character = null;
		}
	}

	public override Color GetMeshColor() {
		if (disabled) return Color.gray;
		if (removed) return Color.black;
		if (activeCharacter != character) return Color.blue;
		return highlighted && active ? Color.red : Color.white;
	}

	protected override void Start() {
		base.Start();
		KMSelectable selfSelectable = GetComponent<KMSelectable>();
		selfSelectable.OnHighlight += () => highlighted = true;
		selfSelectable.OnHighlightEnded += () => highlighted = false;
	}
}
