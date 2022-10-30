import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './App';
import reportWebVitals from './reportWebVitals';
import 'bootstrap/dist/css/bootstrap.min.css';
import { ApiContext, ApiImplementation } from './api';
import Login from './Login';

const root = ReactDOM.createRoot(
	document.getElementById('root') as HTMLElement
);

const api = new ApiImplementation();

let isAuthenticated: boolean | null = null;

const render = () => {
	let domNode: React.ReactNode;

	if (isAuthenticated === null) {
		domNode = <div className="status-text"><h1>Validating authentication...</h1></div>
	} else if(isAuthenticated === false) {
		domNode = (
			<React.StrictMode>
				<ApiContext.Provider value={api}>
					<Login onLoggedIn={() => {
						isAuthenticated = true;
						setupTokenRenewal();
						render();
					}}/>
				</ApiContext.Provider>
			</React.StrictMode>
		)
	} else {
		domNode = (
			<React.StrictMode>
				<ApiContext.Provider value={api}>
					<App />
				</ApiContext.Provider>
			</React.StrictMode>
		);
	}
	root.render(domNode);
}

render();

const authPromise = api.checkAuth();

(async () => {
	isAuthenticated = await authPromise;
	setupTokenRenewal();
	render();
})();

let tokenRenewalTimerId: number | null = null;
const ONE_HOUR_INTERVAL = 1000 /* ms -> s */ * 60 /* s -> min */ * 60 /* min -> h */;

// Refresh token once every hour
function setupTokenRenewal() {
	if (isAuthenticated === false) {
		if (tokenRenewalTimerId !== null)
			window.clearInterval(tokenRenewalTimerId);
		tokenRenewalTimerId = null;
	} else if(isAuthenticated === true) {
		if (tokenRenewalTimerId === null)
			tokenRenewalTimerId = window.setInterval(renewToken, ONE_HOUR_INTERVAL); // 1 hour intervals
	}
}

async function renewToken() {
	if (!await api.renewToken()) {
		isAuthenticated = false;
		setupTokenRenewal();
		render();
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
		document.location.reload();
	})
} else {
	document.addEventListener('visibilitychange', e => {
		if (document.visibilityState === 'visible') {
			if (!api.isWebSocketAlive)
				document.location.reload();
		}
	})
}

api.webSocketDied = () => {
	document.location.reload();
}

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
