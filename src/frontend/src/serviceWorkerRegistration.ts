// This optional code is used to register a service worker.
// register() is not called by default.

import { Api } from "./api";

// This lets the app load faster on subsequent visits in production, and gives
// it offline capabilities. However, it also means that developers (and users)
// will only see deployed updates on subsequent visits to a page, after all the
// existing tabs open on the page have been closed, since previously cached
// resources are updated in the background.

// To learn more about the benefits of this model and instructions on how to
// opt-in, read https://cra.link/PWA

const isLocalhost = Boolean(
	window.location.hostname === 'localhost' ||
	// [::1] is the IPv6 localhost address.
	window.location.hostname === '[::1]' ||
	// 127.0.0.0/8 are considered localhost for IPv4.
	window.location.hostname.match(/^127(?:\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)){3}$/)
);

type Config = {
	onSuccess?: (registration: ServiceWorkerRegistration) => void;
	onUpdate?: (registration: ServiceWorkerRegistration) => void;
};

const runInLocalhost = false;

export async function register(api: Api, config?: Config): Promise<void> {
	console.log("runInLocalhost", runInLocalhost);
	console.log("process.env", process.env);

	if ((runInLocalhost || process.env.NODE_ENV === 'production') && 'serviceWorker' in navigator) {
		// The URL constructor is available in all browsers that support SW.
		const publicUrl = new URL(process.env.PUBLIC_URL, window.location.href);
		if (publicUrl.origin !== window.location.origin) {
			// Our service worker won't work if PUBLIC_URL is on a different origin
			// from what our page is served on. This might happen if a CDN is used to
			// serve assets; see https://github.com/facebook/create-react-app/issues/2374
			return;
		}

		const swUrl = `${process.env.PUBLIC_URL}/service-worker.js`;

		if (isLocalhost) {
			// This is running on localhost. Let's check if a service worker still exists or not.
			await checkValidServiceWorker(swUrl, api, config);

			// Add some additional logging to localhost, pointing developers to the
			// service worker/PWA documentation.
			console.log(
				'This web app is being served cache-first by a service ' +
				'worker. To learn more, visit https://cra.link/PWA'
			);
		} else {
			// Is not localhost. Just register service worker
			await registerValidSW(swUrl, api, config);
		}
	}
}

export async function updateToken(token: string | null): Promise<void> {
	console.log("posting token to service-worker...");
	const activeRegistration = await navigator.serviceWorker.ready;
	activeRegistration.active?.postMessage({type: 'API_TOKEN', token: token});
}

async function registerValidSW(swUrl: string, api: Api, config?: Config) {
	try {
		const registration = await navigator.serviceWorker.register(swUrl);

		registration.onupdatefound = () => {
			const installingWorker = registration.installing;
			if (installingWorker == null) {
				return;
			}
			installingWorker.onstatechange = () => {
				if (installingWorker.state === 'installed') {
					if (navigator.serviceWorker.controller) {
						// At this point, the updated precached content has been fetched,
						// but the previous service worker will still serve the older
						// content until all client tabs are closed.
						console.log(
							'New content is available and will be used when all ' +
							'tabs for this page are closed. See https://cra.link/PWA.'
						);

						// Execute callback
						if (config && config.onUpdate) {
							config.onUpdate(registration);
						}
					} else {
						// At this point, everything has been precached.
						// It's the perfect time to display a
						// "Content is cached for offline use." message.
						console.log('Content is cached for offline use.');

						// Execute callback
						if (config && config.onSuccess) {
							config.onSuccess(registration);
						}
					}
				}
			};
		};
	} catch (error) {
		console.error('Error during service worker registration:', error);
	}
}

async function checkValidServiceWorker(swUrl: string, api: Api, config?: Config) {
	try {
		// Check if the service worker can be found. If it can't reload the page.
		const response = await fetch(swUrl, {
			headers: { 'Service-Worker': 'script' },
		})
		// Ensure service worker exists, and that we really are getting a JS file.
		const contentType = response.headers.get('content-type');
		if (
			response.status === 404 ||
			(contentType != null && contentType.indexOf('javascript') === -1)
		) {
			// No service worker found. Probably a different app. Reload the page.
			navigator.serviceWorker.ready.then((registration) => {
				registration.unregister().then(() => {
					window.location.reload();
				});
			});
		} else {
			// Service worker found. Proceed as normal.
			await registerValidSW(swUrl, api, config);
		}
	} catch (error) {
		console.log('No internet connection found. App is running in offline mode.', error);
	}
}

export async function registerNotifications(api: Api) {
	console.log("awaiting serviceWorker...");
	const activeRegistration = await navigator.serviceWorker.ready;
	
	console.log("registering subscription in worker registration");
	activeRegistration.active?.postMessage({type: 'API_TOKEN', token: api.token});

	let subscription = await activeRegistration.pushManager.getSubscription();
	if (subscription === null) {
		console.log("No subscription since before, calling subscribe...");
		subscription = await activeRegistration.pushManager.subscribe({
			userVisibleOnly: true,
			applicationServerKey: await api.getPublicApplicationServerKey()
		})
	}
	console.log("pushing subscription to api", subscription);
	await api.addPushSubscription(subscription);
}

export async function unregister(api: Api) {
	if ('serviceWorker' in navigator) {
		try {
			const registration = await navigator.serviceWorker.ready;
			const sub = await registration.pushManager.getSubscription();
			if (sub) {
				await sub.unsubscribe();
				await api.removePushSubscription(sub.endpoint);
			}
			registration.unregister();
		} catch (error) {
			console.error(error);
		}
	}
}
