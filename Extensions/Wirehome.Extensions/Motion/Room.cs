﻿using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Collections.Generic;
using Wirehome.Conditions;
using Wirehome.Conditions.Specialized;
using Wirehome.Contracts.Conditions;
using Wirehome.Contracts.Components.Commands;
using Wirehome.Contracts.Components.States;
using Wirehome.Contracts.Environment;
using Wirehome.Contracts.Core;
using Wirehome.Contracts.Components;
using System.Collections.ObjectModel;
using Wirehome.Motion.Model;
using Wirehome.Extensions;
using Wirehome.Extensions.Extensions;

namespace Wirehome.Motion
{
    //TODO Thread safe
    //TODO add change source in event to distinct the source of the change (manual light on or automatic)
    //TODO Manual turn off/on - react if was just after auto, code some automatic reaction (code from manual turn on/off)
    public class Room
    {
        private readonly ConditionsValidator _turnOnConditionsValidator = new ConditionsValidator();
        private readonly ConditionsValidator _turnOffConditionsValidator = new ConditionsValidator();
        private readonly IScheduler _scheduler;
        private readonly MotionConfiguration _motionConfiguration;

        internal IObservable<PowerStateValue> PowerChangeSource { get; } // TODO Add descriptor for some codes for change on/off

        // Configuration parameters
        public string Uid { get; }
        internal IEnumerable<string> Neighbors { get; }
        internal IReadOnlyCollection<Room> NeighborsCache { get; private set; }
        
        // Dynamic parameters
        internal bool AutomationDisabled { get; private set; }
        internal int NumberOfPersonsInArea { get; private set; }
        internal MotionStamp LastMotion { get; } = new MotionStamp();
        internal AreaDescriptor AreaDescriptor { get; }
        internal MotionVector LastVectorEnter { get; private set; }

        private IComponent Lamp { get; }
        private float LightIntensityAtNight { get; }
        private TimeList<DateTimeOffset> _MotionHistory { get; }
        private Probability _PresenceProbability { get; set; } = Probability.Zero;
        private DateTimeOffset _AutomationEnableOn { get; set; }
        private DateTimeOffset _LastManualTurnOn { get; set; }
        private int _PresenseMotionCounter { get; set; }
        private DateTimeOffset? _LastAutoIncrement;
        

        public override string ToString()
        {
            //return $"{MotionDetectorUid} [Last move: {LastMotionTime}] [Persons: {NumberOfPersonsInArea}]";
            //TODO DEBUG
            return $"{Uid} [Last move: {LastMotion}] [Persons: {NumberOfPersonsInArea}] [Lamp: {(Lamp as MotionLamp)?.IsTurnedOn}]";
        }

        public Room(string uid, IEnumerable<string> neighbors, IComponent lamp, IScheduler scheduler,
                                IDaylightService daylightService, IDateTimeService dateTimeService, AreaDescriptor areaDescriptor,
                                MotionConfiguration motionConfiguration)
        {
            Uid = uid ?? throw new ArgumentNullException(nameof(uid));
            Neighbors = neighbors ?? throw new ArgumentNullException(nameof(neighbors));
            Lamp = lamp ?? throw new ArgumentNullException(nameof(lamp));

            if (areaDescriptor.WorkingTime == WorkingTime.DayLight)
            {
                _turnOnConditionsValidator.WithCondition(ConditionRelation.And, new IsDayCondition(daylightService, dateTimeService));
            }
            else if (areaDescriptor.WorkingTime == WorkingTime.AfterDusk)
            {
                _turnOnConditionsValidator.WithCondition(ConditionRelation.And, new IsNightCondition(daylightService, dateTimeService));
            }

            _turnOnConditionsValidator.WithCondition(ConditionRelation.And, new IsEnabledAutomationCondition(this));
            _turnOffConditionsValidator.WithCondition(ConditionRelation.And, new IsEnabledAutomationCondition(this));

            _MotionHistory = new TimeList<DateTimeOffset>(scheduler);

            PowerChangeSource = Lamp.ToPowerChangeSource();

            _scheduler = scheduler;
            _motionConfiguration = motionConfiguration;
            AreaDescriptor = areaDescriptor;
        }

        public void MarkMotion(DateTimeOffset time)
        {
            LastMotion.SetTime(time);
            _MotionHistory.Add(time);
            _PresenseMotionCounter++;
            SetProbability(Probability.Full);

            AutoIncrementForOnePerson(time);
        }

        /// <summary>
        /// When we don't detect motion vector previously but there is move in room and currently we have 0 person so we know that there is a least one
        /// </summary>
        private void AutoIncrementForOnePerson(DateTimeOffset time)
        {
            if (NumberOfPersonsInArea == 0)
            {
                _LastAutoIncrement = time;
                NumberOfPersonsInArea++;
            }
        }

        private void IncrementNumberOfPersons(DateTimeOffset contextTime)
        {
            if (!_LastAutoIncrement.HasValue || contextTime - _LastAutoIncrement > TimeSpan.FromMilliseconds(100))
            {
                NumberOfPersonsInArea++;
            }
        }

        private void DecrementNumberOfPersons()
        {
            if (NumberOfPersonsInArea > 0)
            {
                NumberOfPersonsInArea--;

                if(NumberOfPersonsInArea == 0)
                {
                    LastMotion.UnConfuze();
                }
            }
        }

        private void ZeroNumberOfPersons()
        {
            NumberOfPersonsInArea = 0;
        }

        public void Update()
        {
            CheckForTurnOnAutomationAgain();

            RecalculateProbability();
        }

        public void MarkEnter(MotionVector vector)
        {
            LastVectorEnter = vector;
           // UnConfuzeLastMotionFromEnter(vector);
            IncrementNumberOfPersons(vector.End.TimeStamp);
            
        }
        
        public void MarkLeave(MotionVector vector)
        {
           
            DecrementNumberOfPersons();
            //UnConfuzeLastMotionFromLeave(vector);

            if (AreaDescriptor.MaxPersonCapacity == 1)
            {
                SetProbability(Probability.Zero);
            }
            else
            {
                //TODO change this value                                                                                                                                                        
                SetProbability(Probability.FromValue(0.1));
            }
        }
        
        //private void UnConfuzeLastMotionFromEnter(MotionVector vector)
        //{
        //    if (LastMotionTime.Time == vector.End.TimeStamp)
        //    {
        //        LastMotionTime.UnConfuze();
        //    }
        //}

        //private void UnConfuzeLastMotionFromLeave(MotionVector vector)
        //{
        //    if (LastMotionTime.Time == vector.Start.TimeStamp)
        //    {
        //        LastMotionTime.UnConfuze();
        //    }
        //}

        internal IList<MotionPoint> GetMovementsInNeighborhood(MotionVector vector) => NeighborsCache.ToList()
                                                                                                     .AddChained(this)
                                                                                                     .Where(room => room.Uid != vector.Start.Uid)
                                                                                                     .Select(room => room.CanConfuse(vector.End.TimeStamp))
                                                                                                     .Where(y => y != null)
                                                                                                     .ToList();

        
        internal MotionPoint CanConfuse(DateTimeOffset timeOfMotion)
        {
            var lastMotion = LastMotion;

            // If last motion time has same value we have to go back in time for previous value to check real previous
            if (timeOfMotion == lastMotion.Time)
            {
                lastMotion = lastMotion.Previous;
            }

            if(lastMotion?.Time != null && lastMotion.CanConfuze && timeOfMotion.HappendBefore(lastMotion.Time, AreaDescriptor.MotionDetectorAlarmTime))
            {
                return new MotionPoint(Uid, lastMotion.Time.Value);
            }

            return null;
        }

        internal void BuildNeighborsCache(IEnumerable<Room> neighbors) => NeighborsCache = new ReadOnlyCollection<Room>(neighbors.ToList());
        internal void DisableAutomation() => AutomationDisabled = true;
        internal void EnableAutomation() => AutomationDisabled = false;
        internal void DisableAutomation(TimeSpan time)
        {
            DisableAutomation();
            _AutomationEnableOn = _scheduler.Now + time;
        }

        private void RecalculateProbability()
        {
            var probabilityDelta = 1.0 / (AreaDescriptor.TurnOffTimeout.Ticks / _motionConfiguration.PeriodicCheckTime.Ticks);

            SetProbability(_PresenceProbability.Decrease(probabilityDelta));
        }

        private void CheckForTurnOnAutomationAgain()
        {
            if (AutomationDisabled && _scheduler.Now > _AutomationEnableOn)
            {
                EnableAutomation();
            }
        }

        private void ResetStatistics()
        {
            ZeroNumberOfPersons();
            _MotionHistory.ClearOldData(AreaDescriptor.MotionDetectorAlarmTime);
        }
        
        private void SetProbability(Probability probability)
        {
            _PresenceProbability = probability;

            if(_PresenceProbability.IsFullProbability)
            {
                TryTurnOnLamp();
            }
            else if(_PresenceProbability.IsNoProbability)
            {
                TryTurnOffLamp();
            }
        }

        private void TryTurnOnLamp()
        {
            if (CanTurnOnLamp()) Lamp.ExecuteCommand(new TurnOnCommand());
        }

        private void TryTurnOffLamp()
        {
            if (CanTurnOffLamp())
            {
                Lamp.ExecuteCommand(new TurnOffCommand());
                ResetStatistics();
            }
        }

        private bool CanTurnOnLamp() => _turnOnConditionsValidator.Validate() != ConditionState.NotFulfilled;
        private bool CanTurnOffLamp() => _turnOffConditionsValidator.Validate() != ConditionState.NotFulfilled;
    }

}