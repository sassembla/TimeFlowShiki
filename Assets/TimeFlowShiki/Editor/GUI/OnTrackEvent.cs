using UnityEngine;

namespace TimeFlowShiki {
	public class OnTrackEvent {
		public enum EventType : int {
			EVENT_NONE,

			EVENT_SCORE_ADDTIMELINE,

			EVENT_TIMELINE_ADDTACK,
			EVENT_TIMELINE_DELETE,
			EVENT_TIMELINE_BEFORESAVE,
			EVENT_TIMELINE_SAVE,

			EVENT_TACK_MOVING,
			EVENT_TACK_MOVED,
			EVENT_TACK_MOVED_AFTER,
			EVENT_TACK_DELETED,
			EVENT_TACK_BEFORESAVE,
			EVENT_TACK_SAVE,

			EVENT_OBJECT_SELECTED,
			EVENT_UNSELECTED,
		}

		public readonly EventType eventType;
		public readonly string activeObjectId;
		public readonly int frame;

		public OnTrackEvent (OnTrackEvent.EventType eventType, string activeObjectId, int frame=-1) {
			this.eventType = eventType;
			this.activeObjectId = activeObjectId;
			this.frame = frame;
		}

		public OnTrackEvent Copy () {
			return new OnTrackEvent(this.eventType, this.activeObjectId, this.frame);
		}
	}
}