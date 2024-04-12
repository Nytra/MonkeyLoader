﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonkeyLoader.Events
{
    /// <summary>
    /// Delegate for the <see cref="ICancelableEventSource{TEvent}.Dispatching"/> event.
    /// </summary>
    /// <typeparam name="TEvent">The type representing the cancelable event arguments.</typeparam>
    /// <param name="eventData">An object containing all the relevant information for the events, including canceling the action.</param>
    public delegate void CancelableEventDispatching<in TEvent>(TEvent eventData)
        where TEvent : class, ICancelableEvent;

    /// <summary>
    /// Delegate for the <see cref="IEventSource{TEvent}.Dispatching"/> event.
    /// </summary>
    /// <typeparam name="TEvent">The type representing the event arguments.</typeparam>
    /// <param name="eventData">An object containing all the relevant information for the event.</param>
    public delegate void EventDispatching<in TEvent>(TEvent eventData)
        where TEvent : class;

    /// <summary>
    /// Defines the interface for sources of a certain type of cancelable events.
    /// </summary>
    /// <typeparam name="TEvent">The type of cancelable events generated by this source.</typeparam>
    public interface ICancelableEventSource<out TEvent> where TEvent : class, ICancelableEvent
    {
        /// <summary>
        /// Needs to be invoked when an event is generated with a new instance of the cancelable event.
        /// </summary>
        /// <remarks>
        /// When the event's <see cref="ICancelableEvent.Canceled">Canceled</see>
        /// property is <c>true</c>, the default action should be prevented from happening.
        /// </remarks>
        public event CancelableEventDispatching<TEvent>? Dispatching;
    }

    /// <summary>
    /// Defines the interface for sources of a certain type of events.
    /// </summary>
    /// <typeparam name="TEvent">The type of events generated by this source.</typeparam>
    public interface IEventSource<out TEvent> where TEvent : class
    {
        /// <summary>
        /// Needs to be invoked when an event is generated with a new instance of the event.
        /// </summary>
        public event EventDispatching<TEvent>? Dispatching;
    }
}