using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using MiniJSONForTimeFlowShiki;
 

namespace TimeFlowShiki {
	public class TimeFlowShikiGUIWindow : EditorWindow {
		[SerializeField] private List<ScoreComponent> scores = new List<ScoreComponent>();

		private DateTime lastLoaded = DateTime.MinValue;
		
		private struct ManipulateTargets {
			public List<string> activeObjectIds;

			public ManipulateTargets (List<string> activeObjectIds) {
				this.activeObjectIds = activeObjectIds;
			}
		}
		private ManipulateTargets manipulateTargets = new ManipulateTargets(new List<string>());

		private float selectedPos;
		private int selectedFrame;
		private float cursorPos;
		private float scrollPos;
		private bool repaint;

		private GUIStyle activeFrameLabelStyle;
		private GUIStyle activeConditionValueLabelStyle;

		private struct ManipulateEvents {
			public bool keyLeft;
			public bool keyRight;
			public bool keyUp;
			public bool keyDown;
		}
		private ManipulateEvents manipulateEvents = new ManipulateEvents();

		private List<OnTrackEvent> eventStacks = new List<OnTrackEvent>();

		/**
			Menu item for AssetGraph.
		*/   
		[MenuItem("Window/TimeFlowShiki")]
		static void ShowEditor() {
			EditorWindow.GetWindow<TimeFlowShikiGUIWindow>();
		}

		public void OnEnable () {
			InitializeResources();

			// handler for Undo/Redo
			Undo.undoRedoPerformed += () => {
				SaveData();
				Repaint();
			};
			
			ScoreComponent.Emit = Emit;
			TimelineTrack.Emit = Emit;
			TackPoint.Emit = Emit;


			InitializeScoreView();
		}

		private void InitializeScoreView () {
			this.titleContent = new GUIContent("TimelineKit");

			this.wantsMouseMove = true;
			this.minSize = new Vector2(600f, 300f);

			this.scrollPos = 0;

			ReloadSavedData();
		}

		private void ReloadSavedData () {
			/*
				load saved data.
			*/
			var dataPath = Path.Combine(Application.dataPath, TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_FILEPATH);
			var deserialized = new Dictionary<string, object>();
			var lastModified = DateTime.Now;
			
			if (File.Exists(dataPath)) {
				// load
				deserialized = LoadData(dataPath);

				var lastModifiedStr = deserialized[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_LASTMODIFIED] as string;
				lastModified = Convert.ToDateTime(lastModifiedStr);
			}

			/*
				do nothing if json does not modified after load.
			*/
			if (lastModified == lastLoaded) return;
			lastLoaded = lastModified;

			var indexPoint = new Vector2(0, 0);
			var visibleTrackWidth = this.position.width - indexPoint.x;
			
			if (deserialized.Any()) scores = LoadScores(deserialized);

			// load demo data then save it.
			if (!scores.Any()) {
				var firstAuto = GenerateFirstScore();
				scores.Add(firstAuto);

				SaveData();
			}

			
			SetActiveScore(0);
		}

		private List<ScoreComponent> LoadScores (Dictionary<string, object> deserialized) {
			var newScores = new List<ScoreComponent>();

			var scoresList = deserialized[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORES] as List<object>;

			foreach (var score in scoresList) {
				var scoreDict = score as Dictionary<string, object>;
				var scoreId = scoreDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_ID] as string;
				var scoreTitle = scoreDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_TITLE] as string;
				var scoreTimelines = scoreDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_TIMELINES] as List<object>;

				var currentTimelines = new List<TimelineTrack>();
				foreach (var scoreTimeline in scoreTimelines) {
					var scoreTimelineDict = scoreTimeline as Dictionary<string, object>;

					var timelineTitle = scoreTimelineDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TIMELINE_TITLE] as string;
					var timelineTacks = scoreTimelineDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TIMELINE_TACKS] as List<object>;

					var currentTacks = new List<TackPoint>();
					foreach (var timelineTack in timelineTacks) {
						var timelineTacksDict = timelineTack as Dictionary<string, object>;

						var tackTitle = timelineTacksDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_TITLE] as string;
						var tackStart = Convert.ToInt32(timelineTacksDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_START]);
						var tackSpan = Convert.ToInt32(timelineTacksDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_SPAN]);

						var newTack = new TackPoint(currentTacks.Count, tackTitle, tackStart, tackSpan);

						currentTacks.Add(newTack);
					}

					var newTimeline = new TimelineTrack(currentTimelines.Count, timelineTitle, currentTacks);
					currentTimelines.Add(newTimeline);
				}
				var newScore = new ScoreComponent(scoreId, scoreTitle, currentTimelines);
				newScores.Add(newScore);
			}
			return newScores;
		}

		private Dictionary<string, object> LoadData (string dataPath) {
			var dataStr = string.Empty;
			
			using (var sr = new StreamReader(dataPath)) {
				dataStr = sr.ReadToEnd();
			}
			return Json.Deserialize(dataStr) as Dictionary<string, object>;
		}

		/**
			convert score - timeline - tack datas to data tree.
		*/
		private void SaveData() {
			var lastModified = DateTime.Now;
			var currentScores = scores;
			var currentScoreList = new List<object>();

			foreach (var score in currentScores) {

				var timelineList = new List<object>();
				foreach (var timeline in score.timelineTracks) {

					var tackList = new List<object>();
					foreach (var tack in timeline.tackPoints) {
						var tackDict = new Dictionary<string, object>{
							{TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_TITLE, tack.title},
							{TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_START, tack.start},
							{TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_SPAN, tack.span}
						};

						tackList.Add(tackDict);
					}

					var timelineDict = new Dictionary<string, object>{
						{TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TIMELINE_TITLE, timeline.title},
						{TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TIMELINE_TACKS, tackList}
					};

					timelineList.Add(timelineDict);
				}

				var scoreObject = new Dictionary<string, object>{
					{TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_ID, score.id},
					{TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_TITLE, score.title},
					{TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_TIMELINES, timelineList}
				};

				currentScoreList.Add(scoreObject);
			}

			var data = new Dictionary<string, object>{
				{TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_LASTMODIFIED, lastModified.ToString()},
				{TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORES, currentScoreList}
			};

			var dataStr = Json.Serialize(data);
			var targetDirPath = Path.Combine(Application.dataPath, TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_PATH);
			var targetFilePath = Path.Combine(Application.dataPath, TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_FILEPATH);

			if (!Directory.Exists(targetDirPath)) {
				Directory.CreateDirectory(targetDirPath);
			}

			using (var sw = new StreamWriter(targetFilePath)) {
				sw.Write(dataStr);
			}
		}

		/**
			initialize textures.
		*/
		private void InitializeResources () {
			TimeFlowShikiGUISettings.tickTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TICK, typeof(Texture2D)) as Texture2D;
			TimeFlowShikiGUISettings.timelineHeaderTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TRACK_HEADER_BG, typeof(Texture2D)) as Texture2D;
			TimeFlowShikiGUISettings.conditionLineBgTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_CONDITIONLINE_BG, typeof(Texture2D)) as Texture2D;

			TimeFlowShikiGUISettings.frameTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TRACK_FRAME_BG, typeof(Texture2D)) as Texture2D;

			TimeFlowShikiGUISettings.whitePointTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TACK_WHITEPOINT, typeof(Texture2D)) as Texture2D;
			TimeFlowShikiGUISettings.grayPointTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TACK_GRAYPOINT, typeof(Texture2D)) as Texture2D;

			TimeFlowShikiGUISettings.whitePointSingleTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TACK_WHITEPOINT_SINGLE, typeof(Texture2D)) as Texture2D;
			TimeFlowShikiGUISettings.grayPointSingleTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TACK_GRAYPOINT_SINGLE, typeof(Texture2D)) as Texture2D;

			TimeFlowShikiGUISettings.activeTackBaseTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TACK_ACTIVE_BASE, typeof(Texture2D)) as Texture2D;

			activeFrameLabelStyle = new GUIStyle();
			activeFrameLabelStyle.normal.textColor = Color.white;

			activeConditionValueLabelStyle = new GUIStyle();
			activeConditionValueLabelStyle.fontSize = 11;
			activeConditionValueLabelStyle.normal.textColor = Color.white;
		}


		private int drawCounter = 0;
		private void Update () {
			drawCounter++;

			if (drawCounter % 5 != 0) return;


			if (10000 < drawCounter) drawCounter = 0;
			
			var consumed = false;
			// emit events.
			if (manipulateEvents.keyLeft) {
				SelectPreviousTack();
				consumed = true;	
			}
			if (manipulateEvents.keyRight) {
				SelectNextTack();
				consumed = true;
			}

			if (manipulateEvents.keyUp) {
				SelectAheadObject();
				consumed = true;
			}
			if (manipulateEvents.keyDown) {
				SelectBelowObject();
				consumed = true;
			}
			
			// renew.
			if (consumed) manipulateEvents = new ManipulateEvents();
		}

		private void SelectPreviousTack () {
			if (!HasValidScore()) return;

			var score = GetActiveScore();

			if (manipulateTargets.activeObjectIds.Any()) {
				if (manipulateTargets.activeObjectIds.Count == 1) {
					score.SelectPreviousTackOfTimelines(manipulateTargets.activeObjectIds[0]);
				} else {
					// select multiple objects.
				}
			}

			if (!manipulateTargets.activeObjectIds.Any()) return;

			var currentSelectedFrame = score.GetStartFrameById(manipulateTargets.activeObjectIds[0]);
			if (0 <= currentSelectedFrame) {
				FocusToFrame(currentSelectedFrame);
			}
		}

		private void SelectNextTack () {
			if (!HasValidScore()) return;

			var score = GetActiveScore();
			if (manipulateTargets.activeObjectIds.Any()) {
				if (manipulateTargets.activeObjectIds.Count == 1) {
					score.SelectNextTackOfTimelines(manipulateTargets.activeObjectIds[0]);
				} else {
					// select multiple objects.
				}
			}

			if (!manipulateTargets.activeObjectIds.Any()) return;
			
			var currentSelectedFrame = score.GetStartFrameById(manipulateTargets.activeObjectIds[0]);
			if (0 <= currentSelectedFrame) {
				FocusToFrame(currentSelectedFrame);
			}
		}

		private void SelectAheadObject () {
			if (!HasValidScore()) return;

			var score = GetActiveScore();

			// if selecting object is top, select tick. unselect all objects.
			if (score.IsActiveTimelineOrContainsActiveObject(0)) {
				// var activeFrame = score.GetStartFrameById(manipulateTargets.activeObjectIds[0]);

				Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_UNSELECTED, null));
				return;
			}

			if (!manipulateTargets.activeObjectIds.Any()) return;
			score.SelectAboveObjectById(manipulateTargets.activeObjectIds[0]);
			
			var currentSelectedFrame = score.GetStartFrameById(manipulateTargets.activeObjectIds[0]);
			if (0 <= currentSelectedFrame) {
				FocusToFrame(currentSelectedFrame);
			}
		}

		private void SelectBelowObject () {
			if (!HasValidScore()) return;

			var score = GetActiveScore();
				
			if (manipulateTargets.activeObjectIds.Any()) {
				score.SelectBelowObjectById(manipulateTargets.activeObjectIds[0]);
				var currentSelectedFrame = score.GetStartFrameById(manipulateTargets.activeObjectIds[0]);
				if (0 <= currentSelectedFrame) {
					FocusToFrame(currentSelectedFrame);
				}
				return;
			}

			/*
				choose tack of first timeline under tick.
			*/
			score.SelectTackAtFrame(selectedFrame);
		}
		
		int activeAutoIndex = 0;

		/**
			draw GUI
	   	*/
		private void OnGUI() {
			var viewWidth = this.position.width;
			var viewHeight = this.position.height;

			GUI.BeginGroup(new Rect(0, 0, viewWidth, viewHeight));
			{
				DrawAutoConponent(viewWidth);
			}
			GUI.EndGroup();
		}

		private void DrawAutoConponent (float viewWidth) {
			var changedInDraw = false;

			var xScrollIndex = -scrollPos;
			var yOffsetPos = 0f;


			// draw header.
			var inspectorRect = DrawConditionInspector(xScrollIndex, 0, viewWidth);

			yOffsetPos += inspectorRect.y + inspectorRect.height;

			if (HasValidScore()) {
				var activeAuto = GetActiveScore();
				// draw timelines
				DrawTimelines(activeAuto, yOffsetPos, xScrollIndex, viewWidth);

				yOffsetPos += activeAuto.TimelinesTotalHeight();

				// draw tick
				DrawTick();
			}

			var useEvent = false;
			
			
			switch (Event.current.type) {
				// mouse event handling.
				case EventType.MouseDown: {
					var touchedFrameCount = TimelineTrack.GetFrameOnTimelineFromAbsolutePosX(scrollPos + (Event.current.mousePosition.x - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN));
					if (touchedFrameCount < 0) touchedFrameCount = 0;
					selectedPos = touchedFrameCount * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
					selectedFrame = touchedFrameCount;
					repaint = true;

					Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_UNSELECTED, null));

					useEvent = true;
					break;
				}
				case EventType.ContextClick: {
					ShowContextMenu();
					useEvent = true;
					break;
				}
				case EventType.MouseUp: {

					// right click.
					if (Event.current.button == 1) {
						ShowContextMenu();
						useEvent = true;
						break;
					}

					var touchedFrameCount = TimelineTrack.GetFrameOnTimelineFromAbsolutePosX(scrollPos + (Event.current.mousePosition.x - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN));
					if (touchedFrameCount < 0) touchedFrameCount = 0;
					selectedPos = touchedFrameCount * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
					selectedFrame = touchedFrameCount;
					repaint = true;
					useEvent = true;
					break;
				}
				case EventType.MouseDrag: {
					var pos = scrollPos + (Event.current.mousePosition.x - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN);
					if (pos < 0) pos = 0;
					selectedPos = pos - ((TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 2f) - 1f);
					selectedFrame = TimelineTrack.GetFrameOnTimelineFromAbsolutePosX(pos);

					FocusToFrame(selectedFrame);

					repaint = true;
					useEvent = true;
					break;
				}

				// scroll event handling.
				case EventType.ScrollWheel: {
					if (0 != Event.current.delta.x) {
						scrollPos = scrollPos + (Event.current.delta.x * 2);
						if (scrollPos < 0) scrollPos = 0;
						
						repaint = true;
					}
					useEvent = true;
					break;
				}

				// key event handling.
				case EventType.KeyDown: {
					switch (Event.current.keyCode) {
						case KeyCode.LeftArrow: {
							if (manipulateTargets.activeObjectIds.Count == 0) {
								
								selectedFrame = selectedFrame - 1;
								if (selectedFrame < 0) selectedFrame = 0;
								selectedPos = selectedFrame * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
								repaint = true;

								FocusToFrame(selectedFrame);
							}
							manipulateEvents.keyLeft = true;
							useEvent = true;
							break;
						}
						case KeyCode.RightArrow: {
							if (manipulateTargets.activeObjectIds.Count == 0) {
								selectedFrame = selectedFrame + 1;
								selectedPos = selectedFrame * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
								repaint = true;

								FocusToFrame(selectedFrame);
							}
							manipulateEvents.keyRight = true;
							useEvent = true;
							break;
						}
						case KeyCode.UpArrow: {
							manipulateEvents.keyUp = true;
							useEvent = true;
							break;
						}
						case KeyCode.DownArrow: {
							manipulateEvents.keyDown = true;
							useEvent = true;
							break;
						}
					}
					break;
				}


			}

			// update cursor pos
			cursorPos = selectedPos - scrollPos;

			

			if (repaint) HandleUtility.Repaint();

			if (eventStacks.Any()) {
				foreach (var onTrackEvent in eventStacks) EmitAfterDraw(onTrackEvent);
				eventStacks.Clear();
				SaveData();
			}

			if (useEvent) Event.current.Use();
		}

		private void ShowContextMenu () {
			var nearestTimelineIndex = 0;// fixed. should change by mouse position.
			
			var menu = new GenericMenu();

			if (HasValidScore()) {
				var currentScore = GetActiveScore();
				var scoreId = currentScore.scoreId;

				var menuItems = new Dictionary<string, OnTrackEvent.EventType>{
					{"Add New Timeline", OnTrackEvent.EventType.EVENT_SCORE_ADDTIMELINE}
				};

				foreach (var key in menuItems.Keys) {
					var eventType = menuItems[key];
					menu.AddItem(
						new GUIContent(key),
						false, 
						() => {
							Emit(new OnTrackEvent(eventType, scoreId, nearestTimelineIndex));
						}
					);
				}
			}

			menu.ShowAsContext();
		}

		
		private ScoreComponent GenerateFirstScore () {
			var tackPoints = new List<TackPoint>();
			tackPoints.Add(new TackPoint(0, TimeFlowShikiGUISettings.DEFAULT_TACK_NAME, 0, 10));

			var timelines = new List<TimelineTrack>();
			timelines.Add(new TimelineTrack(0, TimeFlowShikiGUISettings.DEFAULT_TIMELINE_NAME, tackPoints));

			return new ScoreComponent(TimeFlowShikiGUISettings.DEFAULT_SCORE_ID, TimeFlowShikiGUISettings.DEFAULT_SCORE_INFO, timelines);
		}

		private Rect DrawConditionInspector (float xScrollIndex, float yIndex, float inspectorWidth) {
			var width = inspectorWidth - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN;
			var height = yIndex;
			
			var assumedHeight = height
				 + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT
				 + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMELINE_HEIGHT
				 + AssumeConditionLineHeight();

			GUI.BeginGroup(new Rect(TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN, height, width, assumedHeight));
			{
				var internalHeight = 0f;	

				// count & frame in header.
				{
					TimelineTrack.DrawFrameBG(xScrollIndex, internalHeight, width, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMELINE_HEIGHT, true);
					internalHeight = internalHeight + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMELINE_HEIGHT;
				}
				if (HasValidScore()) {
					var currentScore = GetActiveScore();
					var timelines = currentScore.timelineTracks;
					foreach (var timeline in timelines) {
						if (!timeline.IsExistTimeline) continue;
						internalHeight = internalHeight + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_SPAN;

						DrawConditionLine(0, xScrollIndex, timeline, internalHeight);
						internalHeight = internalHeight + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_HEIGHT;
					}

					if (timelines.Any()) {
						// add footer.
						internalHeight = internalHeight + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_SPAN;
					}
				}
			}
			GUI.EndGroup();

			return new Rect(0, 0, inspectorWidth, assumedHeight);
		}

		private void DrawTimelines (ScoreComponent activeAuto, float yOffsetPos, float xScrollIndex, float viewWidth) {
			BeginWindows();
			activeAuto.DrawTimelines(activeAuto, yOffsetPos, xScrollIndex, viewWidth);
			EndWindows();
		}

		private void DrawTick () {
			GUI.BeginGroup(new Rect(TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN,0f, position.width - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN, position.height));
			{
				// tick
				GUI.DrawTexture(new Rect(cursorPos + (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 2f) - 1f, 0f, 3f, position.height), TimeFlowShikiGUISettings.tickTex);
				
				// draw frame count.
				if (selectedFrame == 0) {
					GUI.Label(new Rect(cursorPos + 5f, 1f, 10f, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT), "0", activeFrameLabelStyle);
				} else {
					var span = 0;
					var selectedFrameStr = selectedFrame.ToString();
					if (2 < selectedFrameStr.Length) span = ((selectedFrameStr.Length - 2) * 8) / 2;
					GUI.Label(new Rect(cursorPos + 2 - span, 1f, selectedFrameStr.Length * 10, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT), selectedFrameStr, activeFrameLabelStyle);
				}
			}
			GUI.EndGroup();
		}
		
		private float AssumeConditionLineHeight () {
			var height = 0f;

			if (HasValidScore()) {
				var currentScore = GetActiveScore();
				var timelines = currentScore.timelineTracks;
				for (var i = 0; i < timelines.Count; i++) {
					if (!timelines[i].IsExistTimeline) continue;
					height = height + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_SPAN;
					height = height + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_HEIGHT;
				}

				if (timelines.Any()) {
					// add footer.
					height = height + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_SPAN;
				}
			}

			return height;
		}

		private void DrawConditionLine (float xOffset, float xScrollIndex, TimelineTrack timeline, float yOffset) {
			foreach (var tack in timeline.tackPoints) {
				if (!tack.IsExistTack) continue;

				var start = tack.start;
				var span = tack.span;
				
				var startPos = xOffset + xScrollIndex + (start * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
				var length = span * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
				var tex = tack.GetColorTex();
								
				// draw background.
				if (tack.IsActive()) {
					var condtionLineBgRect = new Rect(startPos, yOffset - 1, length, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_HEIGHT + 2);
					GUI.DrawTexture(condtionLineBgRect, TimeFlowShikiGUISettings.conditionLineBgTex);
				} else {
					var condtionLineBgRect = new Rect(startPos, yOffset + 1, length, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_HEIGHT - 2);
					GUI.DrawTexture(condtionLineBgRect, TimeFlowShikiGUISettings.conditionLineBgTex);
				}

				// fill color.
				var condtionLineRect = new Rect(startPos + 1, yOffset, length - 2, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_HEIGHT);
				GUI.DrawTexture(condtionLineRect, tex);
			}

			// draw timelime text
			foreach (var tack in timeline.tackPoints) {
				var title = tack.title;
				var start = tack.start;
				var span = tack.span;

				if (start <= selectedFrame && selectedFrame < start + span) {
					GUI.Label(new Rect(cursorPos + (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 2f) + 3f, yOffset - 5f, title.Length * 10f, 20f), title, activeConditionValueLabelStyle);
				}
			}
		}
		
		private void Emit (OnTrackEvent onTrackEvent) {
			var type = onTrackEvent.eventType;
			// tack events.
			switch (type) {
				case OnTrackEvent.EventType.EVENT_UNSELECTED: {
					manipulateTargets = new ManipulateTargets(new List<string>());

					Undo.RecordObject(this, "Unselect");

					var activeAuto = GetActiveScore();
					activeAuto.DeactivateAllObjects();
					Repaint();
					return;
				}
				case OnTrackEvent.EventType.EVENT_OBJECT_SELECTED: {
					manipulateTargets = new ManipulateTargets(new List<string>{onTrackEvent.activeObjectId});
					
					var activeAuto = GetActiveScore();

					Undo.RecordObject(this, "Select");
					activeAuto.ActivateObjectsAndDeactivateOthers(manipulateTargets.activeObjectIds);
					Repaint();
					return;
				}

				/*
					auto events.
				*/
				case OnTrackEvent.EventType.EVENT_SCORE_ADDTIMELINE: {
					var activeAuto = GetActiveScore();
					var tackPoints = new List<TackPoint>();
					var newTimeline = new TimelineTrack(activeAuto.timelineTracks.Count, "New Timeline", tackPoints);
					
					Undo.RecordObject(this, "Add Timeline");

					activeAuto.timelineTracks.Add(newTimeline);
					return;
				}
				

				/*
					timeline events.
				*/
				case OnTrackEvent.EventType.EVENT_TIMELINE_ADDTACK: {
					eventStacks.Add(onTrackEvent.Copy());
					return;
				}
				case OnTrackEvent.EventType.EVENT_TIMELINE_DELETE: {
					var targetTimelineId = onTrackEvent.activeObjectId;
					var activeAuto = GetActiveScore();
					
					Undo.RecordObject(this, "Delete Timeline");

					activeAuto.DeleteObjectById(targetTimelineId);
					Repaint();
					SaveData();
					return;
				}				
				case OnTrackEvent.EventType.EVENT_TIMELINE_BEFORESAVE: {
					Undo.RecordObject(this, "Update Timeline Title");
					return;
				}

				case OnTrackEvent.EventType.EVENT_TIMELINE_SAVE: {
					SaveData();
					return;
				}


				/*
					tack events.
				*/
				case OnTrackEvent.EventType.EVENT_TACK_MOVING: {
					var movingTackId = onTrackEvent.activeObjectId;

					var activeAuto = GetActiveScore();

					activeAuto.SetMovingTackToTimelimes(movingTackId);
					break;
				}
				case OnTrackEvent.EventType.EVENT_TACK_MOVED: {

					Undo.RecordObject(this, "Move Tack");

					return;
				}
				case OnTrackEvent.EventType.EVENT_TACK_MOVED_AFTER: {
					var targetTackId = onTrackEvent.activeObjectId;

					var activeAuto = GetActiveScore();
					var activeTimelineIndex = activeAuto.GetTackContainedTimelineIndex(targetTackId);
					if (0 <= activeTimelineIndex) {
						activeAuto.timelineTracks[activeTimelineIndex].UpdateByTackMoved(targetTackId);

						Repaint();
						SaveData();
					}
					return;
				}
				case OnTrackEvent.EventType.EVENT_TACK_DELETED: {
					var targetTackId = onTrackEvent.activeObjectId;
					var activeAuto = GetActiveScore();

					Undo.RecordObject(this, "Delete Tack");

					activeAuto.DeleteObjectById(targetTackId);
					Repaint();
					SaveData();
					return;
				}

				case OnTrackEvent.EventType.EVENT_TACK_BEFORESAVE: {
					Undo.RecordObject(this, "Update Tack Title");
					return;
				}

				case OnTrackEvent.EventType.EVENT_TACK_SAVE: {
					SaveData();
					return;
				}
				
				default: {
					Debug.LogError("no match type:" + type);
					break;
				}
			}
		}

		
		public void SetActiveScore (int index) {
			scores[index].SetActive();
		}

		/**
			Undo,Redoを元に、各オブジェクトのInspectorの情報を更新する
		*/
		public void ApplyDataToInspector () {
			foreach (var score in scores) score.ApplyDataToInspector();
		}


		private bool HasValidScore () {
			if (scores.Any()) {
				foreach (var score in scores) {
					if (score.IsExistScore) return true;
				}
			}
			return false;
		}

		private ScoreComponent GetActiveScore () {
			foreach (var score in scores) {
				if (!score.IsExistScore) continue;
				if (score.IsActive()) return score;
			}
			throw new Exception("no active auto found.");
		}

		private void EmitAfterDraw (OnTrackEvent onTrackEvent) {
			var type = onTrackEvent.eventType;
			switch (type) {
				case OnTrackEvent.EventType.EVENT_TIMELINE_ADDTACK: {
					var targetTimelineId = onTrackEvent.activeObjectId;
					var targetFramePos = onTrackEvent.frame;
					
					var activeAuto = GetActiveScore();

					Undo.RecordObject(this, "Add Tack");

					activeAuto.AddNewTackToTimeline(targetTimelineId, targetFramePos);
					return;
				}
			}
		}

		

		private void FocusToFrame (int focusTargetFrame) {
			var leftFrame = (int)Math.Round(scrollPos / TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
			var rightFrame = (int)(((scrollPos + (position.width - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN)) / TimeFlowShikiGUISettings.TACK_FRAME_WIDTH) - 1);
			
			// left edge of view - leftFrame - rightFrame - right edge of view

			if (focusTargetFrame < leftFrame) {
				scrollPos = scrollPos - ((leftFrame - focusTargetFrame) * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
				return;
			}

			if (rightFrame < focusTargetFrame) {
				scrollPos = scrollPos + ((focusTargetFrame - rightFrame) * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
				return;
			}
		}



		public static bool IsTimelineId (string activeObjectId) {
			if (activeObjectId.StartsWith(TimeFlowShikiGUISettings.ID_HEADER_TIMELINE)) return true;
			return false;
		}

		public static bool IsTackId (string activeObjectId) {
			if (activeObjectId.StartsWith(TimeFlowShikiGUISettings.ID_HEADER_TACK)) return true;
			return false;
		}
	}

}
