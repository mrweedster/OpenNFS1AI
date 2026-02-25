using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenNFS1
{
	class PlayerRaceStats
	{
		public int CurrentLap { get; private set; }
		DateTime _currentLapStartTime = DateTime.MinValue;
		public List<TimeSpan> LapTimes = new List<TimeSpan>();
		public bool HasPassedLapHalfwayPoint;
		public int Position { get; set; }

		public void OnLapStarted()
		{
			if (_currentLapStartTime != DateTime.MinValue)
			{
				LapTimes.Add(new TimeSpan(DateTime.Now.Ticks - _currentLapStartTime.Ticks));
			}
			CurrentLap++;
			_currentLapStartTime = DateTime.Now;
			HasPassedLapHalfwayPoint = false;
		}

		public TimeSpan CurrentLapTime
		{
			get { return new TimeSpan(DateTime.Now.Ticks - _currentLapStartTime.Ticks); }
		}
	}
}