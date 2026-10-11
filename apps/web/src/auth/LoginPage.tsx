import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ApiError, toErrorMessage } from '../api/client';
import { authApi } from '../api/auth';
import { Field } from '../components/Field';
import { saveSession } from './sessionStore';
import { useSession } from './useSession';

interface LoginErrors {
  email?: string;
  password?: string;
}

interface LocationState {
  from?: { pathname: string; search?: string; hash?: string };
}

/** Where to go after signing in: the page the visitor wanted, or the ticket list. */
function destination(state: unknown): string {
  const from = (state as LocationState | null)?.from;

  if (!from?.pathname || from.pathname === '/login') {
    return '/tickets';
  }

  return `${from.pathname}${from.search ?? ''}${from.hash ?? ''}`;
}

function validate(email: string, password: string): LoginErrors {
  const errors: LoginErrors = {};

  if (email.trim() === '') {
    errors.email = 'Enter your email address.';
  } else if (!/^\S+@\S+\.\S+$/.test(email.trim())) {
    errors.email = 'Enter a valid email address.';
  }

  if (password === '') {
    errors.password = 'Enter your password.';
  }

  return errors;
}

/** Signs an agent in. Invalid input never reaches the API. */
export function LoginPage() {
  const session = useSession();
  const location = useLocation();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<LoginErrors>({});
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  if (session) {
    return <Navigate to={destination(location.state)} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitError(undefined);

    const validationErrors = validate(email, password);
    setErrors(validationErrors);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSigningIn(true);

    try {
      const response = await authApi.login(email.trim(), password);

      saveSession({ token: response.token, expiresAtUtc: response.expiresAtUtc, user: response.user });
      void navigate(destination(location.state), { replace: true });
    } catch (caught) {
      setPassword('');
      setSubmitError(
        caught instanceof ApiError && caught.status === 401
          ? 'The email or password is not correct.'
          : toErrorMessage(caught, 'Could not sign in.'),
      );
    } finally {
      setIsSigningIn(false);
    }
  };

  return (
    <main className="login">
      <h1>Northwind Support</h1>

      <form className="card form" onSubmit={submit} noValidate aria-labelledby="login-heading">
        <h2 id="login-heading">Sign in</h2>

        <Field id="login-email" label="Email" error={errors.email}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="email"
              autoComplete="username"
              value={email}
              disabled={isSigningIn}
              onChange={(event) => setEmail(event.target.value)}
            />
          )}
        </Field>

        <Field id="login-password" label="Password" error={errors.password}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="password"
              autoComplete="current-password"
              value={password}
              disabled={isSigningIn}
              onChange={(event) => setPassword(event.target.value)}
            />
          )}
        </Field>

        {submitError && (
          <p className="field__error" role="alert">
            {submitError}
          </p>
        )}

        <div className="button-row">
          <button type="submit" className="button button--primary" disabled={isSigningIn}>
            {isSigningIn ? 'Signing in...' : 'Sign in'}
          </button>
        </div>
      </form>
    </main>
  );
}
