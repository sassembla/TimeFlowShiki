using UnityEngine;
using System.Collections.Generic;

namespace TimeFlowShiki {
	public class TackPointInspector : ScriptableObject {
		public TackPoint tackPoint;

		public void UpdateTackPoint (TackPoint tackPoint) {
			this.tackPoint = tackPoint;
		}
	}
}