import { useEffect, useState } from 'react';
import './App.css';
import { Board } from './components/Board';
import BoardAdmin from './components/BoardAdmin';
import { Board as BoardModel, Lane, Task } from './models/board';
import Tabs from 'react-bootstrap/Tabs';
import Tab from 'react-bootstrap/Tab';
import { Api, WebSocketMessage, withApi } from './api';

interface Props {
	api: Api;
}

const App = (props: Props) => {
	const { api } = props;

	const [board, setBoard] = useState<BoardModel | null>(null);

	useEffect(() => {
		const fetchData = async () => {
			try {
				const newBoard = await api.getBoard();
				setBoard(p => newBoard);
			} catch (err: any) {
				alert(err.message);
			}
		}

		fetchData();
		api.connectWebSocket(async (message: WebSocketMessage) => {
			if (message.type === "board") {
				setBoard(() => message.board);
			}
		});
	}, [api]);


	const onTaskDrop = async (board: BoardModel, taskId: string, task: Task, lane: Lane, index: number) => {
		try {
			// Update the local state
			try {
				const newBoard = moveTask(board, taskId, lane, index);
				if (!newBoard)
					return;

				setBoard(newBoard);
			} catch(error) {
				console.error("Failed to move task in internal state. ", error);
			}

			// And let's call into the server and do it too (state will be reversed if it didn't work)
			await api.moveTask(taskId, lane, index);
		} catch (err: any) {
			alert(err.message);
		}
	}

	return (
		<>
			{board && <Tabs>
				<Tab eventKey="board" title="Board">
					<Board board={board} onTaskDrop={onTaskDrop}/>
				</Tab>

				<Tab eventKey="admin" title="Admin">
					<BoardAdmin board={board} onBoardUpdated={newBoard => setBoard(newBoard)}/>
				</Tab>
			</Tabs>}
			{!board && <div>Loading data...</div>}
		</>
	);
}

export default withApi(App);

function findLaneAndIndex(board: BoardModel, taskId: string): [lane: Lane, index: number] {
	let index = board.readyLaneTasks.indexOf(taskId);
	if (index >= 0)
		return [Lane.Ready, index];

	index = board.inProgressLaneTasks.indexOf(taskId);
	if (index >= 0)
		return [Lane.InProgress, index];
	
	index = board.doneLaneTasks.indexOf(taskId);
	if (index >= 0)
		return [Lane.Done, index];

	index = board.inactiveLaneTasks.indexOf(taskId);
	if (index >= 0)
		return [Lane.Inactive, index];

	throw Error(`Unknown task ${taskId}`);
}

function removeTask(laneTasks: string[], index: number): string[] {
	return [...laneTasks.slice(0, index), ...laneTasks.slice(index + 1)];
}

function insertTask(laneTasks: string[], index: number, taskId: string): string[] {
	return [...laneTasks.slice(0, index), taskId, ...laneTasks.slice(index)];
}

function moveTaskInLane(laneTasks: string[], taskId: string, prevIndex: number | null, index: number | null): string[] {
	if (prevIndex === null && index !== null)
		return insertTask(laneTasks, index, taskId);
	
	if (prevIndex !== null && index === null)
		return removeTask(laneTasks, prevIndex);

	if (prevIndex !== null && index !== null)
		return insertTask(removeTask(laneTasks, prevIndex), index, taskId);

	return laneTasks;
}

function moveTask(board: BoardModel, taskId: string, lane: Lane, index: number): BoardModel | null {
	const [prevLane, prevIndex] = findLaneAndIndex(board, taskId);

	// Don't mess about with the inactive lane (it's a server only lane)
	if (lane === Lane.Inactive || prevLane === Lane.Inactive)
		return null;

	// If no change, then do nothing
	if (prevLane === lane && prevIndex === index)
		return null;
	
	return {
		...board,
		readyLaneTasks: moveTaskInLane(board.readyLaneTasks, taskId, prevLane === Lane.Ready ? prevIndex : null, lane === Lane.Ready ? index : null),
		inProgressLaneTasks: moveTaskInLane(board.inProgressLaneTasks, taskId, prevLane === Lane.InProgress ? prevIndex : null, lane === Lane.InProgress ? index : null),
		doneLaneTasks: moveTaskInLane(board.doneLaneTasks, taskId, prevLane === Lane.Done ? prevIndex : null, lane === Lane.Done ? index : null)
	}
}