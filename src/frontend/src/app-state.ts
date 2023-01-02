export enum State {
	Start = "Start",
	Checking = "Checking",
	Connecting = "Connecting",
	FetchingBoard = "FetchingBoard",
	Running = "Running",
	WantCredentials = "WantCredentials"
}

export enum Event {
	Check = "Check",
	Connect = "Connect",
	Connected = "Connected",
	Reconnect = "Reconnect",
	BoardFetched = "BoardFetched",
	AuthNotOk = "AuthNotOk"
}

type Action = () => void;
interface Transition {
	nextState: State;
	action: Action | null
}

type StateChangedObserver = (oldState: State, newState: State) => void;

export class StateMachine {
	private state: State | null = null;
	private matrix: Map<State, Map<Event, Transition>>;

	stateChangedObserver: StateChangedObserver | null = null;

	constructor() {
		this.matrix = new Map<State, Map<Event, Transition>>();
	}

	set currentState(state: State) {
		this.state = state;
	}

	get currentState(): State {
		if (this.state === null)
			throw new Error("Invalid operation: current/start state is not set!");
		return this.state;
	}

	addTransition(state: State, event: Event, nextState: State, action?: Action) {
		let transitions = this.matrix.get(state);
		if (!transitions)
			this.matrix.set(state, transitions = new Map<Event, Transition>());
		
		let transition = transitions.get(event);
		if (transition)
			throw new Error("State event transition has already been defined!");

		transitions.set(event, { nextState, action: action ?? null })
	}

	execute(event: Event): State {
		if (this.state === null)
			throw new Error("Invalid operation: current/start state is not set!");

		const transitions = this.matrix.get(this.state);
		if (!transitions)
			throw new Error("Invalid operation: no transition defined for state");

		const transition = transitions.get(event);
		if (!transition)
			throw new Error(`Invalid operation: no transition defined for state (${this.state}) and event (${event}) combination`);

		const oldState = this.state;
		this.state = transition.nextState;

		if (transition.action)
			transition.action();

		if (this.stateChangedObserver)
			this.stateChangedObserver(oldState, transition.nextState);

		return transition.nextState;
	}
}