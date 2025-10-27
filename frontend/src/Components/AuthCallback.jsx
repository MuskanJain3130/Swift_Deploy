import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';const updatePlatformToken = async (userId, platform, token) => {
  try {
    const response = await axios.post(
      `http://localhost:5280/api/user/${userId}/tokens/${platform}`, 
      token, 
      {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('jwtToken')}` 
        }
      }
    );
    console.log(response.data);
  } catch (error) {
    console.error('Error updating token:', error.response?.data || error.message);
  }
};
const AuthCallback = () => {
  const hasRun = useRef(false);
  const navigate = useNavigate();
  useEffect(() => {
    if (hasRun.current) return;
    hasRun.current = true;
    let token;
    const cookies = document.cookie.split('; ');
    for (const cookie of cookies) {
      const [name, value] = cookie.split('=');
      if (name === 'GitHubAccessToken') {
        token = value;
        console.log(token);
        axios.post("http://localhost:5280/api/user/login/github/callback", null, {
        headers: {
          Authorization: `Bearer ${token}`
        }
      })
      .then(response => {
        if (response.data.requiresProfileCompletion === true) {
          navigate('/complete-profile/' + response.data.userId);
        } else {
          localStorage.setItem('github_access_token', token);
          localStorage.setItem('user', JSON.stringify(response.data.user));
          localStorage.setItem('jwtToken', response.data.token);
          updatePlatformToken(response.data.user.id, 'github', token).then(() => {
            console.log('Token updated successfully');
            navigate('/projects');
          });
        }
      })
      .catch(error => {
        console.error('Authentication failed:', error);
        navigate('/register');
      });
        break;
      }
    }

      console.error('No token found in cookies');
      navigate('/');
    
  }, []);

  return (
    <div>
      <p>Authentication complete. Redirecting...</p>
    </div>
  );
};

export default AuthCallback;
export {updatePlatformToken};