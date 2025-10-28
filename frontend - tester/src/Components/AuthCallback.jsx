// src/components/AuthCallback.jsx
// ...
import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

const AuthCallback = () => {
  const navigate = useNavigate();

  useEffect(() => {
    let token;
    const cookies = document.cookie.split('; ');
    for (const cookie of cookies) {
      const [name, value] = cookie.split('=');
      if (name === 'GitHubAccessToken') {
        console.log('Found token in cookies:', value);
        token = value;
        break;
      }
    }

    console.log('Token from cookies:', token);
      if (token) {
        localStorage.setItem('github_access_token',token);
        navigate('/repos'); 
      }
    else {
      console.error('No token found in cookies');
      navigate('/');
    }
  }, []);

  return (
    <div>
      <p>Authentication complete. Redirecting...</p>
    </div>
  );
};

export default AuthCallback;