import { duration, Duration } from "moment";
import { State, Task } from "../models/board";
import { setTaskState } from "./board-algorithms";

test('it works', () => {
	const board = {
		tasks: [
			dailyItem("Släng sopor"),
			dailyItem("Rengör slasken"),
			dailyItem("Röj diskbänken"),
			dailyItem("Torka av ytor (kök)"),
			dailyItem("Lägg tvätt i tvättkorg"),
			dailyItem("Röj i 15 min."),
			dailyItem("Rensa kattlådor"),
			dailyItem("Plocka i köket"),

			// Varje veckagrejer
			weeklyItem("Dammsug"),
			weeklyItem("Skura golv"),
			weeklyItem("Återvinning"),
			weeklyItem("Byt handdukar"),
			weeklyItem("Dammtorka ytor"),
			weeklyItem("Byt disktrasa"),
			weeklyItem("Vattna blommor"),
			weeklyItem("Rensa pappershögar"),
			weeklyItem("Handfat, toa, spegel")			
		]
	};

	const newBoard = setTaskState(board, board.tasks[3], State.InProgress);

	newBoard.tasks.slice(0, newBoard.tasks.length - 1).map(t => t.state).forEach(state => expect(state).toBe(State.Ready));
	newBoard.tasks.slice(newBoard.tasks.length - 1).map(t => t.state).forEach(state => expect(state).toBe(State.InProgress));

	const newBoard2 = setTaskState(newBoard, newBoard.tasks[0], State.InProgress);
	newBoard2.tasks.slice(0, newBoard2.tasks.length - 2).map(t => t.state).forEach(state => expect(state).toBe(State.Ready));
	newBoard2.tasks.slice(newBoard2.tasks.length - 2).map(t => t.state).forEach(state => expect(state).toBe(State.InProgress));
})

const item = (x: string, period: Duration) => ({
	id: x,
	title: x,
	description: "Dolor ipsum alea acta jest, caveat emptor regulus terra firma chip chop hej svejs.",
	state: State.Ready,
	schedule: {
		type: "periodic-calendar",
		start: new Date(),
		period: period
	},
	lastChange: new Date()
} as Task);

const dailyItem = (x: string) => item(x, duration(1, 'days'));
const weeklyItem = (x: string) => item(x, duration(7, 'days'));
