import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './App';
import reportWebVitals from './reportWebVitals';
import 'bootstrap/dist/css/bootstrap.min.css';
import { ApiContext, ApiImplementation, WebSocketMessage } from './api';
import Login from './Login';
import { State, Event, StateMachine } from './app-state';
import { delay } from './delay';
import { Board } from './models/board';
import * as serviceWorkerRegistration from './serviceWorkerRegistration';

const root = ReactDOM.createRoot(
	document.getElementById('root') as HTMLElement
);

let tokenRenewalTimerId: number | null = null;
const ONE_HOUR_INTERVAL = 1000 /* ms -> s */ * 60 /* s -> min */ * 60 /* min -> h */;

const api = new ApiImplementation(window.location, localStorage.getItem("jwt"));
let board: Board | null = null;
let serviceWorkerRegistered = false;

const showCause = process.env.NODE_ENV && process.env.NODE_ENV === 'development' || true
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
stateMachine.addTransition(State.Running, Event.Reconnect, State.Checking, () => check());
stateMachine.addTransition(State.FetchingBoard, Event.Reconnect, State.Checking, () => check());

stateMachine.stateChangedObserver = (oldState, newState) => {
	render(`${oldState} -> ${newState}`);
}

stateMachine.execute(Event.Check); // Kick it off!

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
		await api.connectWebSocket(async (message: WebSocketMessage) => {
			if (message.type === "board") {
				board = message.board;
				render("Board received on web socket");
			}
		});
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
		if (!serviceWorkerRegistered) {
			// If you want your app to work offline and load faster, you can change
			// unregister() to register() below. Note this comes with some pitfalls.
			// Learn more about service workers: https://cra.link/PWA
			serviceWorkerRegistration.register(api);
		}

		serviceWorkerRegistration.updateToken(api.token);
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

const hasNotificationPermissions = () => Notification.permission === "granted" 

const requestNotificationPermissions = async () => {
	if (await Notification.requestPermission() === "granted") {
		await serviceWorkerRegistration.registerNotifications(api);
	}
	render("Notification permissions handled");
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
						<App board={board} hasNotificationPermissions={hasNotificationPermissions()}
						     requestNotificationPermissions={requestNotificationPermissions}
						     boardUpdated={newBoard => {
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
	} else {
		serviceWorkerRegistration.updateToken(api.token);
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
			if (!api.isWebSocketAlive)
				stateMachine.execute(Event.Reconnect);
		}
	})
}

api.webSocketDied = () => {
	stateMachine.execute(Event.Reconnect);
}


// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
