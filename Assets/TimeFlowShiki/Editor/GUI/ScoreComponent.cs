using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace TimeFlowShiki {
	[Serializable] public class ScoreComponent {
		public static Action<OnTrackEvent> Emit;

		[SerializeField] private ScoreComponentInspector scoreComponentInspector;

		[CustomEditor(typeof(ScoreComponentInspector))]
		public class ScoreComponentInspectorGUI : Editor {
			public override void OnInspectorGUI () {
				var insp = (ScoreComponentInspector)target;

				var title = insp.title;
				GUILayout.Label("title:" + title);
			}
		}

		[SerializeField] public bool IsExistScore;
		[SerializeField] private bool active;
		[SerializeField] public List<TimelineTrack> timelineTracks;

		// this id is for idenitify at editing.
		[SerializeField] public string scoreId;
		
		// this id is for name of this auto.
		[SerializeField] public string id;
		[SerializeField] public string title;

		
		public ScoreComponent () {}


		public ScoreComponent (string id, string title, List<TimelineTrack> timelineTracks) {
			this.IsExistScore = true;
			this.active = false;

			this.scoreId = TimeFlowShikiGUISettings.ID_HEADER_SCORE + Guid.NewGuid().ToString();

			this.id = id;
			this.title = title;

			this.timelineTracks = new List<TimelineTrack>(timelineTracks);
		}


		public bool IsActive () {
			return active;
		}

		public void SetActive () {
			active = true;

			ApplyDataToInspector();
		}

		public void ShowInspector () {
			Debug.LogError("autoのinspectorをセットする。");
		}

		public void SetDeactive () {
			active = false;
		}
		

		public void DrawTimelines (ScoreComponent auto, float yOffsetPos, float xScrollIndex, float trackWidth) {
			var yIndex = yOffsetPos;

			for (var windowIndex = 0; windowIndex < timelineTracks.Count; windowIndex++) {
				var timelineTrack = timelineTracks[windowIndex];
				if (!timelineTrack.IsExistTimeline) continue;

				var trackHeight = timelineTrack.DrawTimelineTrack(yOffsetPos, xScrollIndex, yIndex, trackWidth);
				
				// set next y index.
				yIndex = yIndex + trackHeight + TimeFlowShikiGUISettings.TIMELINE_SPAN;
			}
		}

		public float TimelinesTotalHeight () {
			var totalHeight = 0f;
			foreach (var timelineTrack in timelineTracks) {
				totalHeight += timelineTrack.Height();
			}
			return totalHeight;
		}

		public List<TimelineTrack> TimelinesByIds (List<string> timelineIds) {
			var results = new List<TimelineTrack>();
			foreach (var timelineTrack in timelineTracks) {
				if (timelineIds.Contains(timelineTrack.timelineId)) {
					results.Add(timelineTrack);
				}
			}
			return results;
		}

		public TackPoint TackById (string tackId) {
			foreach (var timelineTrack in timelineTracks) {
				var tacks = timelineTrack.tackPoints;
				foreach (var tack in tacks) {
					if (tack.tackId == tackId) {
						return tack;
					}
				}
			}
			return null;
		}
	
		public void SelectAboveObjectById (string currentActiveObjectId) {
			if (TimeFlowShikiGUIWindow.IsTimelineId(currentActiveObjectId)) {
				var candidateTimelines = timelineTracks.Where(timeline => timeline.IsExistTimeline).OrderBy(timeline => timeline.GetIndex()).ToList();
				var currentTimelineIndex = candidateTimelines.FindIndex(timeline => timeline.timelineId == currentActiveObjectId);
				
				if (0 < currentTimelineIndex) {
					var targetTimeline = timelineTracks[currentTimelineIndex - 1];
					Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, targetTimeline.timelineId));
					return;
				}

				return;
			}

			if (TimeFlowShikiGUIWindow.IsTackId(currentActiveObjectId)) {
				/*
					select another timeline's same position tack.
				*/
				var currentActiveTack = TackById(currentActiveObjectId);
				
				var currentActiveTackStart = currentActiveTack.start;
				var currentTimelineId = currentActiveTack.parentTimelineId;

				var aboveTimeline = AboveTimeline(currentTimelineId);
				if (aboveTimeline != null) {
					var nextActiveTacks = aboveTimeline.TacksByStart(currentActiveTackStart);
					if (nextActiveTacks.Any()) {
						var targetTack = nextActiveTacks[0];
						Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, targetTack.tackId));
					} else {
						// no tack found, select timeline itself.
						Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, aboveTimeline.timelineId));
					}
				}
				return;
			}
		}

		public void SelectBelowObjectById (string currentActiveObjectId) {
			if (TimeFlowShikiGUIWindow.IsTimelineId(currentActiveObjectId)) {
				var cursoredTimelineIndex = timelineTracks.FindIndex(timeline => timeline.timelineId == currentActiveObjectId);
				if (cursoredTimelineIndex < timelineTracks.Count - 1) {
					var targetTimelineIndex = cursoredTimelineIndex + 1;
					var targetTimeline = timelineTracks[targetTimelineIndex];
					Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, targetTimeline.timelineId));
				}
				return;
			}

			if (TimeFlowShikiGUIWindow.IsTackId(currentActiveObjectId)) {
				/*
					select another timeline's same position tack.
				*/
				var currentActiveTack = TackById(currentActiveObjectId);
				
				var currentActiveTackStart = currentActiveTack.start;
				var currentTimelineId = currentActiveTack.parentTimelineId;

				var belowTimeline = BelowTimeline(currentTimelineId);
				if (belowTimeline != null) {
					var nextActiveTacks = belowTimeline.TacksByStart(currentActiveTackStart);
					if (nextActiveTacks.Any()) {
						var targetTack = nextActiveTacks[0];
						Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, targetTack.tackId));
					} else {
						// no tack found, select timeline itself.
						Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, belowTimeline.timelineId));
					}
				}
				return;
			}
		}

		private TimelineTrack AboveTimeline (string baseTimelineId) {
			var baseIndex = timelineTracks.FindIndex(timeline => timeline.timelineId == baseTimelineId);
			if (0 < baseIndex) return timelineTracks[baseIndex - 1];
			return null;
		}

		private TimelineTrack BelowTimeline (string baseTimelineId) {
			var baseIndex = timelineTracks.FindIndex(timeline => timeline.timelineId == baseTimelineId);
			if (baseIndex < timelineTracks.Count - 1) return timelineTracks[baseIndex + 1];
			return null;
		}

		public void SelectPreviousTackOfTimelines (string currentActiveObjectId) {
			/*
				if current active id is tack, select previous one.
				and if active tack is the head of timeline, select timeline itself.
			*/
			if (TimeFlowShikiGUIWindow.IsTackId(currentActiveObjectId)) {
				foreach (var timelineTrack in timelineTracks) {
					timelineTrack.SelectPreviousTackOf(currentActiveObjectId);
				}
			}
		}

		public void SelectNextTackOfTimelines (string currentActiveObjectId) {
			// if current active id is timeline, select first tack of that.
			if (TimeFlowShikiGUIWindow.IsTimelineId(currentActiveObjectId)) {
				foreach (var timelineTrack in timelineTracks) {
					if (timelineTrack.timelineId == currentActiveObjectId) {
						timelineTrack.SelectDefaultTackOrSelectTimeline();
					}
				}
				return;
			}

			// if current active id is tack, select next one.
			if (TimeFlowShikiGUIWindow.IsTackId(currentActiveObjectId)) {
				foreach (var timelineTrack in timelineTracks) {
					timelineTrack.SelectNextTackOf(currentActiveObjectId);
				}
			}
		}

		public bool IsActiveTimelineOrContainsActiveObject (int index) {
			if (index < timelineTracks.Count) {
				var currentTimeline = timelineTracks[index];
				if (currentTimeline.IsActive()) return true;
				return currentTimeline.ContainsActiveTack();
			}
			return false;
		}

		public int GetStartFrameById (string objectId) {
			if (TimeFlowShikiGUIWindow.IsTimelineId(objectId)) {
				return -1;
			}

			if (TimeFlowShikiGUIWindow.IsTackId(objectId)) {
				var targetContainedTimelineIndex = GetTackContainedTimelineIndex(objectId);
				if (0 <= targetContainedTimelineIndex) {
					var foundStartFrame = timelineTracks[targetContainedTimelineIndex].GetStartFrameById(objectId);
					if (0 <= foundStartFrame) return foundStartFrame;
				}
			}

			return -1;
		}

		public void SelectTackAtFrame (int frameCount) {
			if (timelineTracks.Any()) {
				var firstTimelineTrack = timelineTracks[0];
				var nextActiveTacks = firstTimelineTrack.TacksByStart(frameCount);
				if (nextActiveTacks.Any()) {
					var targetTack = nextActiveTacks[0];
					Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, targetTack.tackId));
				} else {
					// no tack found, select timeline itself.
					Emit(new OnTrackEvent(OnTrackEvent.EventType.EVENT_OBJECT_SELECTED, firstTimelineTrack.timelineId));
				}
			}	
		}

		public void DeactivateAllObjects () {
			foreach (var timelineTrack in timelineTracks) {
				timelineTrack.SetDeactive();
				timelineTrack.DeactivateTacks();
			}
		}

		public void SetMovingTackToTimelimes (string tackId) {
			foreach (var timelineTrack in timelineTracks) {
				if (timelineTrack.ContainsTackById(tackId)) {
					timelineTrack.SetMovingTack(tackId);
				}
			}
		}

		/**
			set active to active objects, and set deactive to all other objects.
			affect to records of Undo/Redo.
		*/
		public void ActivateObjectsAndDeactivateOthers (List<string> activeObjectIds) {
			foreach (var timelineTrack in timelineTracks) {
				if (activeObjectIds.Contains(timelineTrack.timelineId)) timelineTrack.SetActive();
				else timelineTrack.SetDeactive();

				timelineTrack.ActivateTacks(activeObjectIds);
			}
		}

		public int GetTackContainedTimelineIndex (string tackId) {
			return timelineTracks.FindIndex(timelineTrack => timelineTrack.ContainsTackById(tackId));
		}

		public void AddNewTackToTimeline (string timelineId, int frame) {
			var targetTimeline = TimelinesByIds(new List<string>{timelineId})[0];
			targetTimeline.AddNewTackToEmptyFrame(frame);
		}

		public void DeleteObjectById (string deletedObjectId) {
			foreach (var timelineTrack in timelineTracks) {
				if (TimeFlowShikiGUIWindow.IsTimelineId(deletedObjectId)) {
					if (timelineTrack.timelineId == deletedObjectId) {
						timelineTrack.Deleted();
					}
				}
				if (TimeFlowShikiGUIWindow.IsTackId(deletedObjectId)) {
					timelineTrack.DeleteTackById(deletedObjectId);
				}
			}
		}

		public bool HasAnyValidTimeline () {
			if (timelineTracks.Any()) return true;
			return false;
		}

		public int GetIndexOfTimelineById (string timelineId) {
			return timelineTracks.FindIndex(timeline => timeline.timelineId == timelineId);
		}

		public void ApplyDataToInspector () {
			if (scoreComponentInspector == null) scoreComponentInspector = ScriptableObject.CreateInstance("ScoreComponentInspector") as ScoreComponentInspector;

			scoreComponentInspector.title = title;
		}
	}
}