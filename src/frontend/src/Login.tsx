import { useState } from 'react';
import Button from 'react-bootstrap/Button';
import Form from 'react-bootstrap/Form';
import { Api, withApi } from './api';
import "./Login.css";

interface Props {
	api: Api;
	onLoggedIn: () => void;
}

const Login = (props: Props) => {
	const { api, onLoggedIn } = props;

	const [ username, setUsername ] = useState<string>("");
	const [ password, setPassword ] = useState<string>("");
	const [ loginError, setLoginError ] = useState<string | null>(null);

	const [ loginInProgress, setLogonInProgress ] = useState<boolean>(false);

	const onLogin = async () => {
		setLogonInProgress(true);
		setLoginError(null);
		try {
			if (await api.login(username, password))
				onLoggedIn();
			else
				setLoginError("Invalid credentials.");
		} catch (error) {
			setLoginError("Failed to communicate with server.");
		} finally {
			setLogonInProgress(false);
		}
	}

	return (
		<div id="login-form-container">
			<Form id="login-form">
				<Form.Group>
					<Form.Label>Username</Form.Label>
					<Form.Control type="text" placeholder="Username" value={username}
								onChange={e => setUsername(e.target.value)}
								disabled={loginInProgress}/>
				</Form.Group>
				<Form.Group style={{marginTop: "1em"}}>
					<Form.Label>Password</Form.Label>
					<Form.Control type="password" placeholder="Password" value={password}
								onChange={e => setPassword(e.target.value)}
								disabled={loginInProgress}/>
				</Form.Group>
				<Form.Group style={{marginTop: "1em"}}>
					<Button variant="primary" type="button" onClick={() => onLogin()} disabled={loginInProgress}>Login</Button>
					{loginError && <span style={{marginLeft: "1em", color: "red"}}>Invalid credentials.</span>}
				</Form.Group>
			</Form>
		</div>
	);
}

export default withApi(Login);
