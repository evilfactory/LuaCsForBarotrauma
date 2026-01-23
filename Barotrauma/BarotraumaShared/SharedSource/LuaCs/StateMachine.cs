using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Toolkit.Diagnostics;

namespace Barotrauma.LuaCs;

public class StateMachine<T> where T : Enum
{
    private readonly ConcurrentDictionary<T, State<T>> _states;
    private State<T> _currentState;
    public T CurrentState => _currentState.StateId;
    private bool _errorOnSameStateSelected;
    private readonly AsyncReaderWriterLock _operationsLock = new();
    
    public StateMachine(bool errorOnSameState, T defaultState, Action<State<T>> onEnter, Action<State<T>> onExit)
    {
        _errorOnSameStateSelected = errorOnSameState;
        _states = new ConcurrentDictionary<T, State<T>>();
        var defState = new State<T>(defaultState, onEnter, onExit);
        _currentState = defState;
        _states[defaultState] = defState;
    }
    
    public StateMachine<T> AddState(T stateId, Action<State<T>> onEnter, Action<State<T>> onExit)
    {
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (_states.TryGetValue(stateId, out _))
        {
            ThrowHelper.ThrowArgumentException($"State with id {stateId} already exists.");
        }
        
        _states[stateId] = new State<T>(stateId, onEnterState: onEnter, onExitState: onExit);
        return this;
    }

    public StateMachine<T> RemoveState(T stateId)
    {
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (EqualityComparer<T>.Default.Equals(stateId, CurrentState))
        {
            ThrowHelper.ThrowInvalidOperationException($"State with id {CurrentState} is active. Cannot remove.");
        }
        
        _states.TryRemove(stateId, out _);
        return this;
    }

    public StateMachine<T> AddOrReplaceState(T oldStateId, T newStateId, Action<State<T>> onEnter, Action<State<T>> onExit)
    {
        using var lck = _operationsLock.AcquireReaderLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (EqualityComparer<T>.Default.Equals(oldStateId, CurrentState))
        {
            ThrowHelper.ThrowInvalidOperationException($"State with id {CurrentState} is active. Cannot replace.");
        }
        
        _states[oldStateId] = new State<T>(newStateId, onEnter, onExit);
        return this;
    }

    public StateMachine<T> GotoState(T stateId)
    {
        using var lck = _operationsLock.AcquireWriterLock().ConfigureAwait(false).GetAwaiter().GetResult();
        if (EqualityComparer<T>.Default.Equals(stateId, CurrentState))
        {
            if (_errorOnSameStateSelected)
            {
                ThrowHelper.ThrowInvalidOperationException($"State with id {stateId} is already selected.");
            }

            return this;
        }
        
        if (!_states.TryGetValue(stateId, out var newState))
        {
            ThrowHelper.ThrowArgumentNullException($"Target state with id {stateId} does not exist.");
        }
        
        _currentState.OnExit();
        _currentState = newState;
        _currentState.OnEnter();
        return this;
    }
}

public class State<T> where T : Enum
{
    public T StateId;
    private Action<State<T>> _onEnter, _onExit;
    public State(T stateId, Action<State<T>> onEnterState, Action<State<T>> onExitState)
    {
        StateId = stateId;
        _onEnter = onEnterState;
        _onExit = onExitState;
    }

    public virtual void OnEnter()
    {
        _onEnter?.Invoke(this);
    }

    public virtual void OnExit()
    {
        _onExit?.Invoke(this);
    }
}

