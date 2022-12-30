import jwtDecode, { JwtPayload } from "jwt-decode";
import moment from "moment";
import React from "react";
import { formatDuration, parseDurationStrict } from "./duration";
import { Board, Schedule, Task, BoardAndTask, TaskData, TaskDictionary, Lane, IdentifiedTask } from "./models/board";

export interface Api {
	getBoard(): Promise<Board>;
	moveTask(taskId: string, lane: Lane, index: number): Promise<BoardAndTask>;
	updateTask(taskId: string, task: TaskData): Promise<BoardAndTask>;
	createTask(task: TaskData): Promise<BoardAndTask>;
	deleteTask(taskId: string): Promise<Board>;
	connectWebSocket(callback: (message: WebSocketMessage) => Promise<void>): void;
	checkAuth(): Promise<boolean>;
	login(username: string, password: string): Promise<boolean>;
	renewToken(): Promise<boolean>;
	get isWebSocketAlive(): boolean;

	get webSocketDied(): null | (() => void);
	set webSocketDied(value: null | (() => void));
}

let token: string | null = localStorage.getItem("jwt");

const basePath = (path: string) => `api/${path}`;

const origin = !process.env.NODE_ENV || process.env.NODE_ENV === 'development' ? "https://localhost:7099" : "";
const wsOrigin = !process.env.NODE_ENV || process.env.NODE_ENV === 'development' ? "wss://localhost:7099" : `wss://${window.location.host}`;

// TODO: Make this configurable!
const baseAddress = (path: string) => {
	return `${origin}/${basePath(path)}`
}

const baseWebSocketAddress = (path: string, token: string) => {
	return `${wsOrigin}/${basePath(path)}?token=${encodeURIComponent(token)}`
}

async function opWithoutBody(method: string, path: string): Promise<any> {
	const address = baseAddress(path);

	const headers: any = {
		accept: "application/json"
	};


	if (token) {
		headers.authorization = `Bearer ${token}`
	}

	const response = await fetch(
		address,
		{
			method: method,
			headers: headers
		}
	);
	if (!response.ok)
		throw new Error(`Failed to ${method} ${address}: ${response.status}`);
		
	return await response.json();
}

function get(path: string): Promise<any> {
	return opWithoutBody("get", path);
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
function del(path: string): Promise<any> {
	return opWithoutBody("delete", path);
}

async function opWithBody(method: string, path: string, body: any): Promise<any> {
	const address = baseAddress(path);

	const headers: any = {
		accept: "application/json",
		'Content-Type': 'application/json'
	};


	if (token) {
		headers.authorization = `Bearer ${token}`
	}

	const response = await fetch(
		address,
		{
			method: method,
			headers: headers,
			body: JSON.stringify(body)
		}
	);
	if (!response.ok)
		throw new Error(`Failed to ${method} ${address}: ${response.status}`);
		
	return await response.json();
}

function put(path: string, body: any): Promise<any> {
	return opWithBody("put", path, body);
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
function patch(path: string, body: any): Promise<any> {
	return opWithBody("patch", path, body);
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
function post(path: string, body?: any): Promise<any> {
	return body === undefined ? opWithoutBody("post", path) : opWithBody("post", path, body);
}

const PingInterval = 20 * 1000; // Ping web socket server ever 20 second

export class ApiImplementation implements Api {
	private webSocketDiedHandler: null | (() => void) = null;
	private webSocket: WebSocket | null = null;
	private intervalHandle: number | null = null;

	async getBoard(): Promise<Board> {
		return boardToModel(await get("board"));
	}

	async moveTask(taskId: string, lane: Lane, index: number): Promise<BoardAndTask> {
		return boardAndTaskToModel(await put(`board/task/${encodeURIComponent(taskId)}/move`, { lane: laneToDto(lane), index: index }));
	}

	async updateTask(taskId: string, task: TaskData): Promise<BoardAndTask> {
		return boardAndTaskToModel(await put(`board/task/${encodeURIComponent(taskId)}`, taskUpdateToDto(task)));
	}

	async createTask(task: TaskData): Promise<BoardAndTask> {
		return boardAndTaskToModel(await post("board/task", taskUpdateToDto(task)));
	}

	async deleteTask(taskId: string): Promise<Board> {
		return boardToModel(await del(`board/task/${taskId}`));
	}

	get webSocketDied() {
		return this.webSocketDiedHandler;
	}

	set webSocketDied(value: null | (() => void)) {
		this.webSocketDiedHandler = value;
		if (this.webSocket) {
			this.webSocket.onerror = () => {
				if (this.webSocketDiedHandler)
					this.webSocketDiedHandler();
			}

			this.webSocket.onclose = () => {
				if (this.webSocketDiedHandler)
					this.webSocketDiedHandler();
			}
		}
	}

	connectWebSocket(callback: (message: WebSocketMessage) => Promise<void>): void {
		if (!token)
			throw new Error("No authentication performed. Quitting.");

		if (this.webSocket && this.webSocket.readyState === WebSocket.OPEN)
			return;

		try {
			this.webSocket = new WebSocket(baseWebSocketAddress("web-socket", token));
			this.webSocket.onopen = () => {
				this.intervalHandle = window.setInterval(() => {
					this.pingWebSocket();
				}, PingInterval);
			}
			this.webSocket.onmessage = (evt: MessageEvent) => {
				const obj = JSON.parse(evt.data);

				if ("type" in obj) {
					switch(obj.type) {
						case "board":
							callback({
								type: "board",
								board: boardToModel(obj.payload)
							})
							break;
						case "pong":
							// We sent a ping earlier, server responded with pong!
							break;
					}
				} else {
					console.error("Unknown WS message received");
				}
			}

			this.webSocket.onerror = () => {
				if (this.intervalHandle !== null) {
					window.clearInterval(this.intervalHandle);
					this.intervalHandle = null;
				}

				if (this.webSocketDiedHandler)
					this.webSocketDiedHandler();
			}

			this.webSocket.onclose = () => {
				if (this.intervalHandle !== null) {
					window.clearInterval(this.intervalHandle);
					this.intervalHandle = null;
				}

				if (this.webSocketDiedHandler)
					this.webSocketDiedHandler();
			}
		} catch (error) {
			console.error(error);
		}
	}

	private pingWebSocket(): void {
		if (!token)
			throw new Error("No authentication performed. Quitting.");

		if (!this.webSocket || this.webSocket.readyState !== WebSocket.OPEN)
			throw new Error("Web socket is not connected.");
		
		this.webSocket.send(JSON.stringify({
			type: "ping"
		}));
	}

	async checkAuth(): Promise<boolean> {
		if (token === null)
			return false;

		try {
			const claims = jwtDecode<JwtPayload>(token);
			if (!claims.exp || claims.exp * 1000 < Date.now()) {
				localStorage.removeItem("jwt");
				return false;
			}
		} catch (error) {
			console.error(error);
			return false;
		}

		const address = baseAddress("login/check");
		const response = await fetch(
			address,
			{
				method: "GET",
				headers: {
					accept: "application/json",
					authorization: `Bearer ${token}`
				}
			}
		);

		if (response.ok)
			return true;

		if (response.status === 401)
			return false;

		throw new Error("Failed to check authentication status.");
	}

	async login(username: string, password: string): Promise<boolean> {
		const address = baseAddress("login");
		const response = await fetch(
			address,
			{
				method: "POST",
				headers: {
					accept: "application/json",
					'Content-Type': 'application/json'
				},
				body: JSON.stringify({ username, password })
			}
		);

		if (response.ok) {
			localStorage.setItem("jwt", token = await response.json());
			return true;
		}

		if (response.status === 401)
			return false;

		throw new Error("Failed to login.");
	}

	async renewToken(): Promise<boolean> {
		const address = baseAddress("login/renew-token");
		const response = await fetch(
			address,
			{
				method: "POST",
				headers: {
					accept: "application/json",
					'Content-Type': 'application/json',
					'Authorization': `Bearer ${token}`
				}
			}
		);

		if (response.ok) {
			localStorage.setItem("jwt", token = await response.json());
			return true;
		}

		if (response.status === 401)
			return false;

		throw new Error("Failed to renew token.");
	}

	get isWebSocketAlive(): boolean {
		return this.webSocket?.readyState === 1;
	}
}

export const ApiContext = React.createContext<Api>(undefined!);

type WithApiProps = {
	api: Api;
}

export const withApi = <Props extends object>(
	Component: React.ComponentType<Props>
  ): React.FC<Omit<Props, keyof WithApiProps>> => props => (
	  <ApiContext.Consumer>
		{api => <Component {...props as Props} api={api} />}
	  </ApiContext.Consumer>
	);

type TaskDictionaryDto = { [key: string]: TaskDto }

interface BoardDto {
	readonly tasks: TaskDictionaryDto,
	readonly readyLaneTasks: string[],
	readonly inProgressLaneTasks: string[],
	readonly doneLaneTasks: string[],
	readonly inactiveLaneTasks: string[],
}

interface TaskDto {
	readonly title: string,
	readonly description: string,
	readonly schedule: ScheduleDto,
	readonly lastChange: string
}

export interface IdentifiedTaskDto extends TaskDto {
	readonly id: string;
}

interface TaskDataDto {
	readonly title: string,
	readonly description: string,
	readonly schedule: ScheduleDto,
}

interface BoardAndTaskDto {
	readonly board: BoardDto;
	readonly task: IdentifiedTaskDto;
}

enum LaneDto {
	Inactive = "inactive",
	Ready = "ready",
	InProgress = "in-progress",
	Done = "done"
}

interface PeriodicScheduleFollowingCalendarDto {
	readonly type: "periodic-calendar",
	readonly start: string,
	readonly period: string
}

interface PeriodicScheduleFollowingActivityDto {
	readonly type: "periodic-activity",
	readonly start: string,
	readonly period: string
}

interface OneTimeScheduleDto {
	readonly type: "one-time",
	readonly when: string
}

type ScheduleDto = PeriodicScheduleFollowingCalendarDto | PeriodicScheduleFollowingActivityDto | OneTimeScheduleDto;

function boardToModel(dto: BoardDto): Board {
	const modelTasks: TaskDictionary = {};

	for (const key in dto.tasks) {
		modelTasks[key] = taskToModel(dto.tasks[key]);
	}

	return {
		tasks: modelTasks,
		readyLaneTasks: dto.readyLaneTasks,
		inProgressLaneTasks: dto.inProgressLaneTasks,
		doneLaneTasks: dto.doneLaneTasks,
		inactiveLaneTasks: dto.inactiveLaneTasks
	}
}

function boardAndTaskToModel(dto: BoardAndTaskDto): BoardAndTask {
	return {
		board: boardToModel(dto.board),
		task: identifiedTaskToModel(dto.task)
	}
}

function taskToModel(dto: TaskDto): Task {
	return {
	   	title: dto.title,
	   	description: dto.description,
	   	schedule: scheduleToModel(dto.schedule),
	   	lastChange: parseDate(dto.lastChange)
	}
}

function identifiedTaskToModel(dto: IdentifiedTaskDto): IdentifiedTask {
	return {
		id: dto.id,
	   	title: dto.title,
	   	description: dto.description,
	   	schedule: scheduleToModel(dto.schedule),
	   	lastChange: parseDate(dto.lastChange)
	}
}

function taskUpdateToDto(model: TaskData): TaskDataDto {
	return {
		title: model.title,
		description: model.description,
		schedule: scheduleToDto(model.schedule)
	}
}

function laneToDto(model: Lane): LaneDto {
	return model as any as LaneDto;
}
function scheduleToModel(dto: ScheduleDto): Schedule {
	switch(dto.type) {
		case "one-time":
			return {
				type: "one-time",
				when: parseDate(dto.when)
			}
		case "periodic-activity":
			return {
				type: "periodic-activity",
				period: parseDurationStrict(dto.period),
				start: parseDate(dto.start)
			}
		case "periodic-calendar":
			return {
				type: "periodic-calendar",
				period: parseDurationStrict(dto.period),
				start: parseDate(dto.start)
			}
		default:
			throw new Error(`Unknown schedule type ${(dto as any).type}`)
	}
}

function scheduleToDto(model: Schedule): ScheduleDto {
	switch(model.type) {
		case "one-time":
			return {
				type: "one-time",
				when: moment(model.when).format("yyyy-MM-DD")
			}
		case "periodic-activity":
			return {
				type: "periodic-activity",
				period: formatDuration(model.period),
				start: moment(model.start).format("yyyy-MM-DD")
			}
		case "periodic-calendar":
			return {
				type: "periodic-calendar",
				period: formatDuration(model.period),
				start: moment(model.start).format("yyyy-MM-DD")
			}
		default:
			throw new Error(`Unknown schedule type ${(model as any).type}`)
	}
}

function parseDate(start: string): Date {
	return moment(start).toDate();
}

export interface BoardUpdate {
	type: "board",
	board: Board
}

export type WebSocketMessage = BoardUpdate;