import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';const updatePlatformToken = async (userId, platform, token) => {
  if (!userId || !platform || !token) {
    console.error('Missing required parameters for updatePlatformToken:', { userId, platform, token: token ? 'token-exists' : 'no-token' });
    return;
  }

  try {
    console.log(`Updating ${platform} token for user ${userId}`);
    const response = await axios.post(
      `http://localhost:5280/api/user/${userId}/tokens/${platform}`, 
      token, // Send as raw string
      {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('jwtToken')}`
        }
      }
    );
    console.log('Token update successful:', response.data);
    return response.data;
  } catch (error) {
    if (error.response) {
      console.error('Error updating token - Server responded with:', {
        status: error.response.status,
        data: error.response.data,
        headers: error.response.headers
      });
      
      if (error.response.status === 400 || error.response.status === 500) {
        console.log('Attempting to handle duplicate key error...');
        try {
          const updateResponse = await axios.post(
            `http://localhost:5280/api/user/${userId}/tokens/${platform}`, 
            token,
            {
              headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${localStorage.getItem('jwtToken')}`
              }
            }
          );
          console.log('Token update via PATCH successful:', updateResponse.data);
          return updateResponse.data;
        } catch (patchError) {
          console.error('Failed to update token via PATCH:', patchError.response?.data || patchError.message);
          throw patchError;
        }
      }
    } else if (error.request) {
      console.error('No response received:', error.request);
    } else {
      console.error('Error setting up request:', error.message);
    }
    throw error;
  }
};
const AuthCallback = () => {
  const hasRun = useRef(false);
  const navigate = useNavigate();
  useEffect(() => {
  if (hasRun.current) return;
  hasRun.current = true;

  let token = null;
  const cookies = document.cookie.split('; ');
  for (const cookie of cookies) {
    const [name, value] = cookie.split('=');
    if (name === 'GitHubAccessToken') {
      token = value;
      break;
    }
  }

  if (!token) {
    console.error('No token found in cookies');
    navigate('/');
    return;
  }

  axios.post("http://localhost:5280/api/user/login/github/callback",null, {
    headers: { Authorization: `Bearer ${token}` }
  })
  .then(response => {
    console.log('Auth response:', response.data);
    if (response.data.requiresProfileCompletion) {
      console.log('Profile completion required, redirecting...');
      navigate('/complete-profile/' + response.data.userId);
    } else {
      console.log('Setting tokens in localStorage...');
      localStorage.setItem('github_access_token', token);
      localStorage.setItem('user', JSON.stringify(response.data.user));
      localStorage.setItem('jwtToken', response.data.token);
      console.log('Tokens set, navigating to /projects');
      updatePlatformToken(response.data.user.id, 'github', token).then(() => {
        console.log('Token updated successfully');
        navigate('/projects');
      });
    }
  })
  .catch(error => {
    console.error('Authentication failed:', error);
    if (error.response) {
      console.error('Response data:', error.response.data);
      console.error('Response status:', error.response.status);
      console.error('Response headers:', error.response.headers);
    } else if (error.request) {
      console.error('No response received:', error.request);
    } else {
      console.error('Error message:', error.message);
    }
    navigate('/register');
  });
}, [navigate]); // Added navigate to the dependency array
  return (
    <div>
      <p>Authentication complete. Redirecting...</p>
    </div>
  );
};

export default AuthCallback;
export {updatePlatformToken};