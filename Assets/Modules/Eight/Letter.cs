using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Letter : Character {
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

	public override Color GetMeshColor() {
		return activeCharacter != character ? Color.blue : (highlighted && active ? Color.red : Color.white);
	}

	protected override void Start() {
		base.Start();
		KMSelectable selfSelectable = GetComponent<KMSelectable>();
		selfSelectable.OnHighlight += () => highlighted = true;
		selfSelectable.OnHighlightEnded += () => highlighted = false;
	}
}
