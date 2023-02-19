import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './App';
import reportWebVitals from './reportWebVitals';
import 'bootstrap/dist/css/bootstrap.min.css';
import { ApiContext, ApiImplementation, origin, parse, token } from './api';
import Login from './Login';
import { State, Event, StateMachine } from './app-state';
import { delay } from './delay';
import { Board } from './models/board';
import * as signalR from "@microsoft/signalr";

const root = ReactDOM.createRoot(
	document.getElementById('root') as HTMLElement
);

let signalRConnection: signalR.HubConnection | null = null;


let tokenRenewalTimerId: number | null = null;
const ONE_HOUR_INTERVAL = 1000 /* ms -> s */ * 60 /* s -> min */ * 60 /* min -> h */;

const api = new ApiImplementation();
let board: Board | null = null;

const showCause = process.env.NODE_ENV && process.env.NODE_ENV === 'development'
	? (state: State, cause: string) => console.info(`render: state = ${state}, cause = ${cause}`)
	: () => {}

const stateMachine = new StateMachine();
stateMachine.currentState = State.Start;
stateMachine.addTransition(State.Start, Event.Check, State.Checking, () => check());
stateMachine.addTransition(State.Checking, Event.AuthNotOk, State.WantCredentials, () => authNotOk());
stateMachine.addTransition(State.Checking, Event.Connect, State.Connecting, () => connect());
stateMachine.addTransition(State.Connecting, Event.Connected, State.FetchingBoard, () => connected());
stateMachine.addTransition(State.FetchingBoard, Event.BoardFetched, State.Running, () => boardFetched());
stateMachine.addTransition(State.Connecting, Event.Check, State.Checking, () => check());
stateMachine.addTransition(State.WantCredentials, Event.Connect, State.Connecting, () => connect());
stateMachine.addTransition(State.Running, Event.Reconnect, State.Checking, () => reconnect());
stateMachine.addTransition(State.FetchingBoard, Event.Reconnect, State.Checking, () => reconnect());

stateMachine.stateChangedObserver = (oldState, newState) => {
	render(`${oldState} -> ${newState}`);
}

stateMachine.execute(Event.Check); // Kick it off!

async function reconnect(): Promise<void> {
	stopSignalR();

	await check();
}

async function check(): Promise<void> {
	if (tokenRenewalTimerId !== null)
		window.clearInterval(tokenRenewalTimerId);
	tokenRenewalTimerId = null;

	let retry = false;
	do {
		try {
			const isAuthenticated = await api.checkAuth();
			if (isAuthenticated) {
				stateMachine.execute(Event.Connect);
			} else {
				stateMachine.execute(Event.AuthNotOk);
			}
			return;
		} catch(err) {
			console.error("Failed to check authentication", err);
			await delay(5000);
			retry = true;
		}
	} while(retry);
}

async function connect(): Promise<void> {
	try {
		const hubOptions: signalR.IHttpConnectionOptions = {
			accessTokenFactory: () => token ?? "",
			headers: {
				Authorization: `Bearer ${token}`
			}
		};
		
		
		signalRConnection = new signalR.HubConnectionBuilder()
			.withUrl(origin + "/api/board-hub", hubOptions)
			.withAutomaticReconnect()
			.build();

		signalRConnection.on("BoardUpdated", newBoard => {
			board = parse(newBoard);
			render("Board received on SignalR hub");
		})
		await signalRConnection.start();
		stateMachine.execute(Event.Connected);
	} catch (err) {
		console.error("Failed to connect web socket", err);
		stateMachine.execute(Event.Check);
	}
}

async function authNotOk(): Promise<void> {
}

async function connected(): Promise<void> {
	try {
		board = await api.getBoard();
		stateMachine.execute(Event.BoardFetched);
	} catch (err) {
		console.error("Failed to get board", err);
		stateMachine.execute(Event.Reconnect);
	}
}

async function boardFetched(): Promise<void> {
	tokenRenewalTimerId = window.setInterval(renewToken, ONE_HOUR_INTERVAL); // 1 hour intervals
}

function renderStatusText(text: string) {
	return <div className="status-text"><h1>{text}</h1></div>;
}

function stopSignalR() {
	if (signalRConnection &&
		(signalRConnection.state === signalR.HubConnectionState.Connected ||
		 signalRConnection.state === signalR.HubConnectionState.Connecting ||
		 signalRConnection.state === signalR.HubConnectionState.Reconnecting))
		signalRConnection.stop();
}


function render(cause: string) {
	let domNode: React.ReactNode;

	showCause(stateMachine.currentState, cause);

	switch (stateMachine.currentState) {
		case State.Start:
		case State.Checking:
			domNode = renderStatusText("Validating authentication...");
			break;
		case State.Connecting:
			domNode = renderStatusText("Connecting web socket...");
			break;
		case State.FetchingBoard:
			domNode = renderStatusText("Loading board...");
			break;
		case State.WantCredentials:
			stopSignalR();
			domNode = (
				// <React.StrictMode>
					<ApiContext.Provider value={api}>
						<Login onLoggedIn={() => stateMachine.execute(Event.Connect)}/>
					</ApiContext.Provider>
				// </React.StrictMode>
			)
			break;
		case State.Running:
			domNode = (
				// <React.StrictMode>
					<ApiContext.Provider value={api}>
						<App board={board} boardUpdated={newBoard => {
							board = newBoard;
							render("Application changed the board")
						}}/>
					</ApiContext.Provider>
				// </React.StrictMode>
			);
	}

	root.render(domNode);
}

async function renewToken() {
	if (!await api.renewToken()) {
		stateMachine.execute(Event.Reconnect);
	}
}

// Detect if the web app has been "thawed" - happens when swapping in and out
// of browser on mobiles. Some web browsers like Edge and Chrome can do it on the
// desktop too to conserve memory and CPU. When we are resumed, make sure the
// the web socket is resumed, and that the token is still valid

// Do we have support for the resume event? Chrome and Edge does. We prefer it, because
// it is a lot more reliable, and will only be fired when we actually have been resumed!
// Refresh page as needed!
if ('onresume' in document) {
	document.addEventListener('resume', () => {
		stateMachine.execute(Event.Reconnect);
	})
} else {
	document.addEventListener('visibilitychange', e => {
		if (document.visibilityState === 'visible') {
			stateMachine.execute(Event.Reconnect);
		}
	})
}

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
