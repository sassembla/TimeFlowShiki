using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace TimeFlowShiki {
	[Serializable] public class TimelineTrack {
		public static Action<OnTrackEvent> Emit;

		[SerializeField] private TimelineTrackInspector timelineTrackInspector;

		[CustomEditor(typeof(TimelineTrackInspector))]
		public class TimelineTrackInspectorGUI : Editor {
			public override void OnInspectorGUI () {
				var insp = (TimelineTrackInspector)target;

				var timelineTrack = insp.timelineTrack;
				UpdateTimelineTrackTitle(timelineTrack);
			}

			private void UpdateTimelineTrackTitle (TimelineTrack timelineTrack) {
				var newTitle = EditorGUILayout.TextField("title", timelineTrack.title);
				if (newTitle != timelineTrack.title) {
					timelineTrack.BeforeSave();
					timelineTrack.title = newTitle;
					timelineTrack.Save();
				}
			}
		}

		[SerializeField] private int index;
		
		[SerializeField] public bool IsExistTimeline;
		[SerializeField] public bool active;
		[SerializeField] public string timelineId;

		[SerializeField] public string title;
		[SerializeField] public List<TackPoint> tackPoints = new List<TackPoint>();
		
		private Rect trackRect;
		private Texture2D timelineBaseTexture;

		private float timelineScrollX;

		private GUIStyle timelineConditionTypeLabelStyle;
		private GUIStyle timelineConditionTypeLabelSmallStyle;

		private List<string> movingTackIds = new List<string>();

		public TimelineTrack () {
			InitializeTextResource();


			this.IsExistTimeline = true;


			// set initial track rect.
			var defaultHeight = (TimeFlowShikiGUISettings.TIMELINE_HEIGHT);
			trackRect = new Rect(0, 0, 10, defaultHeight);
		}

		public TimelineTrack (int index, string title, List<TackPoint> tackPoints) {
			InitializeTextResource();

			this.IsExistTimeline = true;

			this.timelineId = TimeFlowShikiGUISettings.ID_HEADER_TIMELINE + Guid.NewGuid().ToString();
			this.index = index;
			this.title = title;
			this.tackPoints = new List<TackPoint>(tackPoints);
			
			// set initial track rect.
			var defaultHeight = (TimeFlowShikiGUISettings.TIMELINE_HEIGHT);
			trackRect = new Rect(0, 0, 10, defaultHeight);

			ApplyTextureToTacks(index);
		}

		private void InitializeTextResource () {
			this.timelineConditionTypeLabelStyle = new GUIStyle();
			timelineConditionTypeLabelStyle.normal.textColor = Color.white;
			timelineConditionTypeLabelStyle.fontSize = 16;
			timelineConditionTypeLabelStyle.alignment = TextAnchor.MiddleCenter;

			timelineConditionTypeLabelSmallStyle = new GUIStyle();
			timelineConditionTypeLabelSmallStyle.normal.textColor = Color.white;
			timelineConditionTypeLabelSmallStyle.fontSize = 10;
			timelineConditionTypeLabelSmallStyle.alignment = TextAnchor.MiddleCenter;
		}

		public void BeforeSave () {
			Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_TIMELINE_BEFORESAVE, this.timelineId));
		}

		public void Save () {
			Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_TIMELINE_SAVE, this.timelineId));
		}

		/*
			get texture for this timeline, then set texture to every tack.
		*/
		public void ApplyTextureToTacks (int texIndex) {
			timelineBaseTexture = GetTimelineTexture(texIndex);
			foreach (var tackPoint in tackPoints) tackPoint.InitializeTackTexture(timelineBaseTexture);
		}

		public static Texture2D GetTimelineTexture (int textureIndex) {
			var color = TimeFlowShikiGUISettings.RESOURCE_COLORS_SOURCES[textureIndex % TimeFlowShikiGUISettings.RESOURCE_COLORS_SOURCES.Count];
			var colorTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
			colorTex.SetPixel(0, 0, color);
			colorTex.Apply();

			return colorTex;
		}

		public float Height () {
			return trackRect.height;
		}

		public int GetIndex () {
			return index;
		}

		public void SetActive () {
			active = true;

			ApplyDataToInspector();
			Selection.activeObject = timelineTrackInspector;
		}

		public void SetDeactive () {
			active = false;
		}

		public bool IsActive () {
			return active;
		}

		public bool ContainsActiveTack () {
			foreach (var tackPoint in tackPoints) {
				if (tackPoint.IsActive()) return true;
			}
			return false;
		}

		public int GetStartFrameById (string objectId) {
			foreach (var tackPoint in tackPoints) {
				if (tackPoint.tackId == objectId) return tackPoint.start;
			}
			return -1;
		}

		public void SetTimelineY (float additional) {
			trackRect.y = trackRect.y + additional;
		}

		public float DrawTimelineTrack (float headWall, float timelineScrollX, float yOffsetPos, float width) {
			this.timelineScrollX = timelineScrollX;

			trackRect.width = width;
			trackRect.y = yOffsetPos;

			if (trackRect.y < headWall) trackRect.y = headWall;

			if (timelineBaseTexture == null) ApplyTextureToTacks(index);

			trackRect = GUI.Window(index, trackRect, WindowEventCallback, string.Empty, "AnimationKeyframeBackground");
			return trackRect.height;
		}

		public float GetY () {
			return trackRect.y;
		}

		public float GetHeight () {
			return trackRect.height;
		}

		private void WindowEventCallback (int id) {
			// draw bg from header to footer.
			{
				if (active) {
					var headerBGActiveRect = new Rect(0f, 0f, trackRect.width, TimeFlowShikiGUISettings.TIMELINE_HEIGHT);
					GUI.DrawTexture(headerBGActiveRect, TimeFlowShikiGUISettings.activeTackBaseTex);

					var headerBGRect = new Rect(1f, 1f, trackRect.width - 1f, TimeFlowShikiGUISettings.TIMELINE_HEIGHT - 2f);
					GUI.DrawTexture(headerBGRect, TimeFlowShikiGUISettings.timelineHeaderTex);
				} else {
					var headerBGRect = new Rect(0f, 0f, trackRect.width, TimeFlowShikiGUISettings.TIMELINE_HEIGHT);
					GUI.DrawTexture(headerBGRect, TimeFlowShikiGUISettings.timelineHeaderTex);
				}
			}

			var timelineBodyY = TimeFlowShikiGUISettings.TIMELINE_HEADER_HEIGHT;

			// timeline condition type box.	
			var conditionBGRect = new Rect(1f, timelineBodyY, TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_WIDTH - 1f, TimeFlowShikiGUISettings.TACK_HEIGHT - 1f);
			if (active) {
				var conditionBGRectInActive = new Rect(1f, timelineBodyY, TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_WIDTH  -1f, TimeFlowShikiGUISettings.TACK_HEIGHT - 1f);
				GUI.DrawTexture(conditionBGRectInActive, timelineBaseTexture);	
			} else {
				GUI.DrawTexture(conditionBGRect, timelineBaseTexture);
			}

			// draw timeline title.
			if (!string.IsNullOrEmpty(title)) {
				if (title.Length < 9) {
					GUI.Label(
						new Rect(
							0f, 
							TimeFlowShikiGUISettings.TIMELINE_HEADER_HEIGHT - 1f, 
							TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_WIDTH, 
							TimeFlowShikiGUISettings.TACK_HEIGHT
						), 
						title,
						timelineConditionTypeLabelStyle
					);
				} else {
					GUI.Label(
						new Rect(
							0f, 
							TimeFlowShikiGUISettings.TIMELINE_HEADER_HEIGHT - 1f, 
							TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_WIDTH, 
							TimeFlowShikiGUISettings.TACK_HEIGHT
						), 
						title,
						timelineConditionTypeLabelSmallStyle
					);
				}
			}
			

			var frameRegionWidth = trackRect.width - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN;

			// draw frame back texture & TackPoint datas on frame.
			GUI.BeginGroup(new Rect(TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN, timelineBodyY, trackRect.width, TimeFlowShikiGUISettings.TACK_HEIGHT));
			{
				DrawFrameRegion(timelineScrollX, 0f, frameRegionWidth);
			}
			GUI.EndGroup();

			var useEvent = false;

			// mouse manipulation.
			switch (Event.current.type) {
				
				case EventType.ContextClick: {
					ShowContextMenu(timelineScrollX);
					useEvent = true;
					break;
				}

				// clicked.
				case EventType.MouseUp: {

					// is right clicked
					if (Event.current.button == 1) {
						ShowContextMenu(timelineScrollX);
						useEvent = true;
						break;
					}

					Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, this.timelineId));
					useEvent = true;
					break;
				}
			}

			// constraints.
			trackRect.x = 0;
			if (trackRect.y < 0) trackRect.y = 0;

			GUI.DragWindow();
			if (useEvent) Event.current.Use();
		}

		private void ShowContextMenu (float scrollX) {
			var targetFrame = GetFrameOnTimelineFromLocalMousePos(Event.current.mousePosition, scrollX);
			var menu = new GenericMenu();

			var menuItems = new Dictionary<string, OnTrackEvent.EventType>{
				{"Add New Tack", OnTrackEvent.EventType.EVENT_TIMELINE_ADDTACK},
				{"Delete This Timeline", OnTrackEvent.EventType.EVENT_TIMELINE_DELETE},

				// not implemented yet.
				// {"Copy This Timeline", OnTrackEvent.EventType.EVENT_TIMELINE_COPY},
				// {"Paste Tack", OnTrackEvent.EventType.EVENT_TACK_PASTE},
				// {"Cut This Timeline", OnTrackEvent.EventType.EVENT_TIMELINE_CUT},
				// {"Hide This Timeline", OnTrackEvent.EventType.EVENT_TIMELINE_HIDE},
			};

			foreach (var key in menuItems.Keys) {
				var eventType = menuItems[key];
				var enable = IsEnableEvent(eventType, targetFrame);
				if (enable) {
					menu.AddItem(
						new GUIContent(key),
						false, 
						() => {
							Emit(new OnTrackEvent(eventType, this.timelineId, targetFrame));
						}
					);
				} else {
					menu.AddDisabledItem(new GUIContent(key));
				}
			}
			menu.ShowAsContext();
		}

		private bool IsEnableEvent (OnTrackEvent.EventType eventType, int frame) {
			switch (eventType) {
				case OnTrackEvent.EventType.EVENT_TIMELINE_ADDTACK: {
					foreach (var tackPoint in tackPoints) {
						if (tackPoint.ContainsFrame(frame)) {
							if (!tackPoint.IsExistTack) return true;
							return false;
						}
					}
					return true;
				}
				case OnTrackEvent.EventType.EVENT_TIMELINE_DELETE: {
					return true;
				}


				default: {
					// Debug.LogError("unhandled eventType IsEnableEvent:" + eventType);
					return false;
				}
			}
		}

		private int GetFrameOnTimelineFromLocalMousePos (Vector2 localMousePos, float scrollX) {
			var frameSourceX = localMousePos.x + Math.Abs(scrollX) - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN;
			return GetFrameOnTimelineFromAbsolutePosX(frameSourceX);
		}

		public static int GetFrameOnTimelineFromAbsolutePosX (float frameSourceX) {
			return (int)(frameSourceX / TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
		}

		private void DrawFrameRegion (float timelineScrollX, float timelineBodyY, float frameRegionWidth) {
			var limitRect = new Rect(0, 0, frameRegionWidth, TimeFlowShikiGUISettings.TACK_HEIGHT);
			
			// draw frame background.
			{
				DrawFrameBG(timelineScrollX, timelineBodyY, frameRegionWidth, TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT, false);
			}

			// draw tack points & label on this track in range.
			{
				var index = 0;
				foreach (var tackPoint in tackPoints) {
					var isUnderEvent = movingTackIds.Contains(tackPoint.tackId);
					if (!movingTackIds.Any()) isUnderEvent = true;

					// draw tackPoint on the frame.
					tackPoint.DrawTack(limitRect, this.timelineId, timelineScrollX, timelineBodyY, isUnderEvent);
					index++;
				}
			}
		}

		public static void DrawFrameBG (float timelineScrollX, float timelineBodyY, float frameRegionWidth, float frameRegionHeight, bool showFrameCount) {
			var yOffset = timelineBodyY;

			// show 0 count.
			if (showFrameCount) {
				if (0 < TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + timelineScrollX) GUI.Label(new Rect(timelineScrollX + 3, 0, 20, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT), "0");
				yOffset = yOffset + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT;
			}

			// draw 1st 1 frame.
			if (0 < TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + timelineScrollX) {
				GUI.DrawTexture(new Rect(timelineScrollX, yOffset, TimeFlowShikiGUISettings.TACK_5FRAME_WIDTH, frameRegionHeight), TimeFlowShikiGUISettings.frameTex);
			}


			var repeatCount = (frameRegionWidth - timelineScrollX) / TimeFlowShikiGUISettings.TACK_5FRAME_WIDTH;
			for (var i = 0; i < repeatCount; i++) {
				var xPos = TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + timelineScrollX + (i * TimeFlowShikiGUISettings.TACK_5FRAME_WIDTH);
				if (xPos + TimeFlowShikiGUISettings.TACK_5FRAME_WIDTH < 0) continue;

				if (showFrameCount) {
					var frameCountStr = ((i + 1) * 5).ToString();
					var span = 0;
					if (2 < frameCountStr.Length) span = ((frameCountStr.Length - 2) * 8) / 2;
					GUI.Label(new Rect(xPos + (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH * 4) - span, 0, frameCountStr.Length * 10, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT), frameCountStr);
				}
				var frameRect = new Rect(xPos, yOffset, TimeFlowShikiGUISettings.TACK_5FRAME_WIDTH, frameRegionHeight);
				GUI.DrawTexture(frameRect, TimeFlowShikiGUISettings.frameTex);
			}
		}

		public void SelectPreviousTackOf (string tackId) {
			var cursoredTackIndex = tackPoints.FindIndex(tack => tack.tackId == tackId);
			
			if (cursoredTackIndex == 0) {
				Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, this.timelineId));
				return;
			}

			var currentExistTacks = tackPoints.Where(tack => tack.IsExistTack).OrderByDescending(tack => tack.start).ToList();
			var currentTackIndex = currentExistTacks.FindIndex(tack => tack.tackId == tackId);

			if (0 <= currentTackIndex && currentTackIndex < currentExistTacks.Count - 1) {
				var nextTack = currentExistTacks[currentTackIndex + 1];
				Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, nextTack.tackId));
			}
		}

		public void SelectNextTackOf (string tackId) {
			var currentExistTacks = tackPoints.Where(tack => tack.IsExistTack).OrderBy(tack => tack.start).ToList();
			var currentTackIndex = currentExistTacks.FindIndex(tack => tack.tackId == tackId);

			if (0 <= currentTackIndex && currentTackIndex < currentExistTacks.Count - 1) {
				var nextTack = currentExistTacks[currentTackIndex + 1];
				Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, nextTack.tackId));
			}
		}

		public void SelectDefaultTackOrSelectTimeline () {
			if (tackPoints.Any()) {
				var firstTackPoint = tackPoints[0];
				Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, firstTackPoint.tackId));
				return;
			}
			
			Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, this.timelineId));
		}

		public void ActivateTacks (List<string> activeTackIds) {
			foreach (var tackPoint in tackPoints) {
				if (activeTackIds.Contains(tackPoint.tackId)) {
					tackPoint.SetActive();
				} else {
					tackPoint.SetDeactive();
				}
			}
		}

		public void DeactivateTacks () {
			foreach (var tackPoint in tackPoints) {
				tackPoint.SetDeactive();
			}
		}

		public List<TackPoint> TacksByIds (List<string> tackIds) {
			var results = new List<TackPoint>();
			foreach (var tackPoint in tackPoints) {
				if (tackIds.Contains(tackPoint.tackId)) {
					results.Add(tackPoint);
				}
			}
			return results;
		}

		/**
			returns the tack which has nearlest start point.
		*/
		public List<TackPoint> TacksByStart (int startPos) {
			var startIndex = tackPoints.FindIndex(tack => startPos <= tack.start);
			if (0 <= startIndex) {
				// if index - 1 tack contains startPos, return it.
				if (0 < startIndex && (startPos <= tackPoints[startIndex-1].start + tackPoints[startIndex-1].span - 1)) {
					return new List<TackPoint>{tackPoints[startIndex-1]};
				}
				return new List<TackPoint>{tackPoints[startIndex]};
			}

			// no candidate found in area, but if any tack exists, select the last of it. 
			if (tackPoints.Any()) {
				return new List<TackPoint>{tackPoints[tackPoints.Count - 1]};
			}
			return new List<TackPoint>();
		}

		public bool ContainsTackById (string tackId) {
			foreach (var tackPoint in tackPoints) {
				if (tackId == tackPoint.tackId) return true;
			}
			return false;
		}

		public void Deleted () {
			IsExistTimeline = false;
		}

		public void UpdateByTackMoved (string tackId) {
			movingTackIds.Clear();
			
			var movedTack = TacksByIds(new List<string>{tackId})[0];

			movedTack.ApplyDataToInspector();

			foreach (var targetTack in tackPoints) {
				if (targetTack.tackId == tackId) continue;
				if (!targetTack.IsExistTack) continue;
			
				// not contained case.
				if (targetTack.start + (targetTack.span - 1) < movedTack.start) continue;
				if (movedTack.start + (movedTack.span - 1) < targetTack.start) continue;

				// movedTack contained targetTack, delete.
				if (movedTack.start <= targetTack.start && targetTack.start + (targetTack.span - 1) <= movedTack.start + (movedTack.span - 1)) {
					Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_TACK_DELETED, targetTack.tackId));
					continue;
				}

				// moved tack's tail is contained by target tack, update.
				// m-tm-t
				if (movedTack.start <= targetTack.start + (targetTack.span - 1) && targetTack.start + (targetTack.span - 1) <= movedTack.start + (movedTack.span - 1)) {
					var resizedSpan = movedTack.start - targetTack.start;
					targetTack.UpdatePos(targetTack.start, resizedSpan);
					continue;
				}

				// moved tack's head is contained by target tack's tail, update.
				// t-mt-m
				if (targetTack.start <= movedTack.start + (movedTack.span - 1) && movedTack.start <= targetTack.start) {
					var newStartPos = movedTack.start + movedTack.span;
					var resizedSpan = targetTack.span - (newStartPos - targetTack.start);
					targetTack.UpdatePos(newStartPos, resizedSpan);
					continue;
				}

				if (targetTack.start < movedTack.start && movedTack.start + movedTack.span < targetTack.start + targetTack.span) {
					var resizedSpanPoint = movedTack.start - 1;
					var resizedSpan = resizedSpanPoint - targetTack.start + 1;
					targetTack.UpdatePos(targetTack.start, resizedSpan);
					continue;
				}
			}
		}

		public void SetMovingTack (string tackId) {
			movingTackIds = new List<string>{tackId};
		}

		public void AddNewTackToEmptyFrame (int frame) {
			var newTackPoint = new TackPoint(
				tackPoints.Count, 
				TimeFlowShikiGUISettings.DEFAULT_TACK_NAME, 
				frame, 
				TimeFlowShikiGUISettings.DEFAULT_TACK_SPAN
			);
			tackPoints.Add(newTackPoint);

			ApplyTextureToTacks(index);
		}

		public void DeleteTackById (string tackId) {
			var deletedTackIndex = tackPoints.FindIndex(tack => tack.tackId == tackId);
			if (deletedTackIndex == -1) return;
			tackPoints[deletedTackIndex].Deleted();
		}

		public void ApplyDataToInspector () {
			if (timelineTrackInspector == null) timelineTrackInspector = ScriptableObject.CreateInstance("TimelineTrackInspector") as TimelineTrackInspector;
			
			timelineTrackInspector.UpdateTimelineTrack(this);

			foreach (var tackPoint in tackPoints) {
				tackPoint.ApplyDataToInspector();
			}
		}
	}
}