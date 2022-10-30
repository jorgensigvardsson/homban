import React, { useEffect, useState } from 'react';
import './App.css';
import { Board } from './components/Board';
import BoardAdmin from './components/BoardAdmin';
import { getTask, setTaskState } from './logic/board-algorithms';
import { Board as BoardModel, State, Task } from './models/board';
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
				setBoard(p => message.board);
			}
		});

	}, [api]);


	const onTaskBeginDrag = (board: BoardModel, task: Task, event: React.DragEvent<HTMLElement>) => {
		event.dataTransfer.setData("task", task.id);
	}

	const onTaskDrop = async (board: BoardModel, requestedState: State, event: React.DragEvent<HTMLElement>) => {
		event.preventDefault();

		const taskId = event.dataTransfer.getData("task");
		const task = getTask(board, taskId);

		// Update the local state
		setBoard(setTaskState(board, task, requestedState));

		try {
			// And let's call into the server and do it too (state will be reversed if it didn't work)
			await api.setTaskState(task.id, requestedState);
		} catch (err: any) {
			alert(err.message);
		}
	}

	return (
		<>
			{board && <Tabs>
				<Tab eventKey="board" title="Board">
					<Board board={board}
						onTaskBeginDrag={(task, event) => onTaskBeginDrag(board, task, event)}
						onTaskDrop={(requestedState, event) => onTaskDrop(board, requestedState, event)}/>
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
