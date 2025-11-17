import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

function NetlifyCallback() {
    const navigate = useNavigate();

  useEffect(() => {
    let token;
    const cookies = document.cookie.split('; ');
    for (const cookie of cookies) {
      const [name, value] = cookie.split('=');
      if (name === 'NetlifyAccessToken') {
        console.log('Found Netlify token in cookies:', value);
        token = value;
        break;
      }
    }

    console.log('Netlify Access Token from cookies:', token);
      if (token) {
        localStorage.setItem('NetlifyAccessToken',token);
        navigate('/deployments'); 
      }
    else {
      console.error('No token found in cookies');
      navigate('/');
    }
  }, []);

  return (
    <div>
      <p>Netlify Authentication complete. Redirecting...</p>
    </div>
  );
}


export default NetlifyCallback

  