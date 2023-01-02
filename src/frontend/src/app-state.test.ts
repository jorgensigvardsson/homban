import { StateMachine, State, Event } from "./app-state";

test("state machine works", () => {
	let called = false;
	const sm = new StateMachine();
	sm.currentState = State.Start;
	sm.addTransition(State.Start, Event.Check, State.Checking, () => { called = true});

	const newState = sm.execute(Event.Check);

	expect(newState).toBe(State.Checking);
	expect(called).toBe(true);
})