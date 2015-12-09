using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;


namespace TimeFlowShiki {
	[Serializable] public class TackPoint {
		public static Action<OnTrackEvent> Emit;

		[SerializeField] private TackPointInspector tackPointInspector;

		[CustomEditor(typeof(TackPointInspector))]
		public class TackPointInspectorGUI : Editor {
			public override void OnInspectorGUI () {
				var insp = (TackPointInspector)target;

				var tackPoint = insp.tackPoint;
				UpdateTackTitle(tackPoint);


				GUILayout.Space(12);

				var start = tackPoint.start;
				GUILayout.Label("start:" + start);

				var span = tackPoint.span;

				var end = start + span - 1;
				GUILayout.Label("end:" + end);

				GUILayout.Label("span:" + span);
			}


			private void UpdateTackTitle (TackPoint tackPoint) {
				var newTitle = EditorGUILayout.TextField("title", tackPoint.title);
				if (newTitle != tackPoint.title) {
					tackPoint.BeforeSave();
					tackPoint.title = newTitle;
					tackPoint.Save();
				}
			}
		}

		[SerializeField] public string tackId;
		[SerializeField] public string parentTimelineId;
		[SerializeField] private int index;
		
		[SerializeField] private bool active = false;
		[SerializeField] public bool IsExistTack = true;

		[SerializeField] public string title;
		[SerializeField] public int start;
		[SerializeField] public int span;

		[SerializeField] private Texture2D tackBackTransparentTex;
		[SerializeField] private Texture2D tackColorTex;

		private Vector2 distance = Vector2.zero;

		private enum TackModifyMode : int {
			NONE,
			
			GRAB_START,
			GRAB_BODY,
			GRAB_END,
			GRAB_HALF,

			DRAG_START,
			DRAG_BODY,
			DRAG_END,
		}
		private TackModifyMode mode = TackModifyMode.NONE;

		private Vector2 dragBeginPoint;


		public TackPoint () {

		}

		public TackPoint (
			int index,
			string title, 
			int start, 
			int span
		) {
			this.tackId = TimeFlowShikiGUISettings.ID_HEADER_TACK + Guid.NewGuid().ToString();
			this.index = index;

			this.IsExistTack = true;

			this.title = title;
			this.start = start;
			this.span = span;
		}

		public Texture2D GetColorTex () {
			return tackColorTex;
		}

		public void InitializeTackTexture (Texture2D baseTex) {
			GenerateTextureFromBaseTexture(baseTex, index);
		}

		public void SetActive () {
			active = true;

			ApplyDataToInspector();
			Selection.activeObject = tackPointInspector;
		}

		public void SetDeactive () {
			active = false;
		}

		public bool IsActive () {
			return active;
		}

		public void DrawTack (Rect limitRect, string parentTimelineId, float startX, float startY, bool isUnderEvent) {
			if (!IsExistTack) return;

			this.parentTimelineId = parentTimelineId;

			var tackBGRect = DrawTackPointInRect(startX, startY);

			var globalMousePos = Event.current.mousePosition;

			var useEvent = false;

			var localMousePos = new Vector2(globalMousePos.x - tackBGRect.x, globalMousePos.y - tackBGRect.y);
			var sizeRect = new Rect(0, 0, tackBGRect.width, tackBGRect.height);

			if (!isUnderEvent) return;

			// mouse event handling.
			switch (mode) {
				case TackModifyMode.NONE: {
					useEvent = BeginTackModify(tackBGRect, globalMousePos);
					break;
				}

				case TackModifyMode.GRAB_START:
				case TackModifyMode.GRAB_BODY:
				case TackModifyMode.GRAB_END:
				case TackModifyMode.GRAB_HALF: {
					useEvent = RecognizeTackModify(globalMousePos);
					break;
				}

				case TackModifyMode.DRAG_START:
				case TackModifyMode.DRAG_BODY:
				case TackModifyMode.DRAG_END: {
					useEvent = UpdateTackModify(limitRect, tackBGRect, globalMousePos);
					break;
				}
			}


			// optional manipulation.
			if (sizeRect.Contains(localMousePos)) {
				switch (Event.current.type) {

					case EventType.ContextClick: {
						ShowContextMenu();
						useEvent = true;
						break;
					}

					// clicked.
					case EventType.MouseUp: {
						// right click.
						if (Event.current.button == 1) {
							ShowContextMenu();
							useEvent = true;
							break;
						}

						Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, tackId));
						useEvent = true;
						break;
					}
				}
			}

			if (useEvent) {
				Event.current.Use();
			}
		}

		public void BeforeSave () {
			Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_TACK_BEFORESAVE, this.tackId, start));
		}

		public void Save () {
			Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_TACK_SAVE, this.tackId, start));
		}


		private void ShowContextMenu () {
			var framePoint = start;
			var menu = new GenericMenu();

			var menuItems = new Dictionary<string, OnTrackEvent.EventType>{
				{"Delete This Tack", OnTrackEvent.EventType.EVENT_TACK_DELETED}
			};

			foreach (var key in menuItems.Keys) {
				var eventType = menuItems[key];
				menu.AddItem(
					new GUIContent(key),
					false, 
					() => {
						Emit(new OnTrackEvent(eventType, this.tackId, framePoint));
					}
				);
			}
			menu.ShowAsContext();
		}

		private void GenerateTextureFromBaseTexture (Texture2D baseTex, int index) {
			var samplingColor = baseTex.GetPixels()[0];
			var rgbVector = new Vector3(samplingColor.r, samplingColor.g, samplingColor.b);
			
			var rotatedVector = Quaternion.AngleAxis(12.5f * index, new Vector3(1.5f * index, 1.25f * index, 1.37f * index)) * rgbVector;
			
			var slidedColor = new Color(rotatedVector.x, rotatedVector.y, rotatedVector.z, 1);

			this.tackBackTransparentTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
			tackBackTransparentTex.SetPixel(0, 0, new Color(slidedColor.r, slidedColor.g, slidedColor.b, 0.5f));
			tackBackTransparentTex.Apply();

			this.tackColorTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
			tackColorTex.SetPixel(0, 0, new Color(slidedColor.r, slidedColor.g, slidedColor.b, 1.0f));
			tackColorTex.Apply();
		}

		private Rect DrawTackPointInRect (float startX, float startY) {
			var tackStartPointX = startX + (start * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
			var end = start + span - 1;
			var tackEndPointX = startX + (end * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);

			var tackBGRect = new Rect(tackStartPointX, startY, span * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + 1f, TimeFlowShikiGUISettings.TACK_HEIGHT);
			
			switch (mode) {
				case TackModifyMode.DRAG_START: {
					tackStartPointX = startX + (start * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH) + distance.x;
					tackBGRect = new Rect(tackStartPointX, startY, span * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + 1f - distance.x, TimeFlowShikiGUISettings.TACK_HEIGHT);
					break;
				}
				case TackModifyMode.DRAG_BODY: {
					tackStartPointX = startX + (start * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH) + distance.x;
					tackEndPointX = startX + (end * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH) + distance.x;
					tackBGRect = new Rect(tackStartPointX, startY, span * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + 1f, TimeFlowShikiGUISettings.TACK_HEIGHT);
					break;
				}
				case TackModifyMode.DRAG_END: {
					tackEndPointX = startX + (end * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH) + distance.x;
					tackBGRect = new Rect(tackStartPointX, startY, span * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + distance.x + 1f, TimeFlowShikiGUISettings.TACK_HEIGHT);
					break;
				}
			}

			

			// draw tack.
			{
				// draw bg.
				var frameBGRect = new Rect(tackBGRect.x, tackBGRect.y, tackBGRect.width, TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT);

				GUI.DrawTexture(frameBGRect, tackBackTransparentTex);
				
				// draw points.
				{
					// tackpoint back line.
					if (span == 1) GUI.DrawTexture(new Rect(tackBGRect.x + (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 3) + 1, startY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 1, (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 3) - 1, 11), tackColorTex); 
					if (1 < span) GUI.DrawTexture(new Rect(tackBGRect.x + (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 2), startY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 1, tackEndPointX - tackBGRect.x, 11), tackColorTex);

					// frame start point.
					DrawTackPoint(start, tackBGRect.x, startY);

					// frame end point.
					if (1 < span) DrawTackPoint(end, tackEndPointX, startY);
				}
				
				var routineComponentY = startY + TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT;

				// routine component.
				{
					var height = TimeFlowShikiGUISettings.ROUTINE_HEIGHT_DEFAULT;
					if (active) GUI.DrawTexture(new Rect(tackBGRect.x, routineComponentY, tackBGRect.width, height), TimeFlowShikiGUISettings.activeTackBaseTex);

					GUI.DrawTexture(new Rect(tackBGRect.x + 1, routineComponentY, tackBGRect.width - 2, height - 1), tackColorTex);
					
					GUI.Label(new Rect(tackBGRect.x + 1, routineComponentY, tackBGRect.width - 2, height - 1), title);
				}
			}

			return tackBGRect;
		}

		private bool BeginTackModify (Rect tackBGRect, Vector2 beginPoint) {
			
			switch (Event.current.type) {
				case EventType.MouseDown: {
					var startRect = new Rect(tackBGRect.x, tackBGRect.y, TimeFlowShikiGUISettings.TACK_FRAME_WIDTH, TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT);
					if (startRect.Contains(beginPoint)) {
						if (span == 1) {
							dragBeginPoint = beginPoint;
							mode = TackModifyMode.GRAB_HALF;
							return true;
						}
						dragBeginPoint = beginPoint;
						mode = TackModifyMode.GRAB_START;
						return true;
					}
					var endRect = new Rect(tackBGRect.x + tackBGRect.width - TimeFlowShikiGUISettings.TACK_FRAME_WIDTH, tackBGRect.y, TimeFlowShikiGUISettings.TACK_FRAME_WIDTH, TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT);
					if (endRect.Contains(beginPoint)) {
						dragBeginPoint = beginPoint;
						mode = TackModifyMode.GRAB_END;
						return true;
					}
					if (tackBGRect.Contains(beginPoint)) {
						dragBeginPoint = beginPoint;
						mode = TackModifyMode.GRAB_BODY;
						return true;
					}
					return false;
				}
			}

			return false;
		}

		private bool RecognizeTackModify (Vector2 mousePos) {
			
			switch (Event.current.type) {
				case EventType.MouseDrag: {
					switch (mode) {
						case TackModifyMode.GRAB_START: {
							mode = TackModifyMode.DRAG_START;
							Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, tackId));
							return true;
						}
						case TackModifyMode.GRAB_BODY: {
							mode = TackModifyMode.DRAG_BODY;
							Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, tackId));
							return true;
						}
						case TackModifyMode.GRAB_END: {
							mode = TackModifyMode.DRAG_END;
							Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, tackId));
							return true;
						}
						case TackModifyMode.GRAB_HALF: {
							if (mousePos.x < dragBeginPoint.x) mode = TackModifyMode.DRAG_START;
							if (dragBeginPoint.x < mousePos.x) mode = TackModifyMode.DRAG_END;
							Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, tackId));
							return true;
						}
					}

					return false;
				}
				case EventType.MouseUp: {
					mode = TackModifyMode.NONE;
					Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, tackId));
					return true;
				}
			}

			return false;
		}
		
		private bool UpdateTackModify (Rect limitRect, Rect tackBGRect, Vector2 draggingPoint) {
			if (!limitRect.Contains(draggingPoint)) {
				ExitUpdate(distance);
				return true;
			}

			// far from bandwidth, exit mode.
			if (draggingPoint.y < 0 || tackBGRect.height + TimeFlowShikiGUISettings.TIMELINE_HEADER_HEIGHT < draggingPoint.y) {
				ExitUpdate(distance);
				return true;
			}
			
			switch (Event.current.type) {
				case EventType.MouseDrag: {
					Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_TACK_MOVING, tackId));
					
					distance = draggingPoint - dragBeginPoint;
					var distanceToFrame = DistanceToFrame(distance.x);

					switch (mode) {
						case TackModifyMode.DRAG_START: {
							// limit 0 <= start
							if ((start + distanceToFrame) < 0) distance.x = -FrameToDistance(start);

							// limit start <= end
							if (span <= (distanceToFrame + 1)) distance.x = FrameToDistance(span - 1);
							break;
						}
						case TackModifyMode.DRAG_BODY: {
							// limit 0 <= start
							if ((start + distanceToFrame) < 0) distance.x = -FrameToDistance(start);
							break;
						}
						case TackModifyMode.DRAG_END: {
							// limit start <= end
							if ((span + distanceToFrame) <= 1) distance.x = -FrameToDistance(span - 1);
							break;
						}
					}

					return true;
				}
				case EventType.MouseUp: {
					ExitUpdate(distance);
					return true;
				}
			}

			return false;
		}

		private void ExitUpdate (Vector2 currentDistance) {
			var distanceToFrame = DistanceToFrame(currentDistance.x);

			Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_TACK_MOVED, tackId));

			switch (mode) {
				case TackModifyMode.DRAG_START: {
					start = start + distanceToFrame;
					span = span - distanceToFrame;
					break;
				}
				case TackModifyMode.DRAG_BODY: {
					start = start + distanceToFrame;
					break;
				}
				case TackModifyMode.DRAG_END: {
					span = span + distanceToFrame;
					break;
				}
			}

			if (start < 0) start = 0;

			Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_TACK_MOVED_AFTER, tackId));

			mode = TackModifyMode.NONE;

			distance = Vector2.zero;
		}

		private int DistanceToFrame (float distX) {
			var distanceToFrame = (int)(distX/TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
			var distanceDelta = distX % TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
			
			// adjust behaviour by frame width.
			if (TimeFlowShikiGUISettings.BEHAVE_FRAME_MOVE_RATIO <= distanceDelta) distanceToFrame = distanceToFrame + 1;
			if (distanceDelta <= -TimeFlowShikiGUISettings.BEHAVE_FRAME_MOVE_RATIO) distanceToFrame = distanceToFrame - 1;
			
			return distanceToFrame;
		}

		private float FrameToDistance (int frame) {
			return TimeFlowShikiGUISettings.TACK_FRAME_WIDTH * frame;
		}

		private void DrawTackPoint (int frame, float pointX, float pointY) {
			if (span == 1) {
				if (frame % 5 == 0 && 0 < frame) {
					GUI.DrawTexture(new Rect(pointX + 2, pointY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 2, TimeFlowShikiGUISettings.TACK_POINT_SIZE, TimeFlowShikiGUISettings.TACK_POINT_SIZE), TimeFlowShikiGUISettings.grayPointSingleTex);
				} else {
					GUI.DrawTexture(new Rect(pointX + 2, pointY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 2, TimeFlowShikiGUISettings.TACK_POINT_SIZE, TimeFlowShikiGUISettings.TACK_POINT_SIZE), TimeFlowShikiGUISettings.whitePointSingleTex);
				}	
				return;
			}

			if (frame % 5 == 0 && 0 < frame) {
				GUI.DrawTexture(new Rect(pointX + 2, pointY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 2, TimeFlowShikiGUISettings.TACK_POINT_SIZE, TimeFlowShikiGUISettings.TACK_POINT_SIZE), TimeFlowShikiGUISettings.grayPointTex);
			} else {
				GUI.DrawTexture(new Rect(pointX + 2, pointY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 2, TimeFlowShikiGUISettings.TACK_POINT_SIZE, TimeFlowShikiGUISettings.TACK_POINT_SIZE), TimeFlowShikiGUISettings.whitePointTex);
			}
		}

		public void Deleted () {
			IsExistTack = false;
		}

		public bool ContainsFrame (int frame) {
			if (start <= frame && frame <= start + span - 1) return true;
			return false;
		}

		public void UpdatePos (int start, int span) {
			this.start = start;
			this.span = span;
			ApplyDataToInspector();
		}


		public void ApplyDataToInspector () {
			if (tackPointInspector == null) tackPointInspector = ScriptableObject.CreateInstance("TackPointInspector") as TackPointInspector;
			
			tackPointInspector.UpdateTackPoint(this);
		}
	}
}