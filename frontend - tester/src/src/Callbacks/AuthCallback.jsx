// import React, { useEffect } from 'react';
// import { useNavigate } from 'react-router-dom';
// import axios from 'axios';

// const AuthCallback = () => {
//   const navigate = useNavigate();

//   useEffect(() => {
//     const fetchUser = async () => {
//       try {
//         const response = await axios.get(
//           `${import.meta.env.REACT_APP_API_BASE_URL}/auth/success`,
//           { withCredentials: true }
//         );
        
//         const userData = response.data;
//         if (userData) {
//           const repoResponse = await axios.get(
//             `${import.meta.env.REACT_APP_API_BASE_URL}/repositories`,
//             { withCredentials: true }
//           );
//           const repos = repoResponse.data;
//           localStorage.setItem("repos", JSON.stringify(repos));
//         }
//         navigate("/");
//       } catch (error) {
//         console.error("Error during authentication:", error);
//         navigate("/");
//       }
//     };

//     fetchUser();
//   }, [navigate]);

//   return <div>Loading...</div>;
// };

// export default AuthCallback;// src/Callbacks/AuthCallback.jsx
import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../Contexts/AuthContext';

const AuthCallback = () => {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  useEffect(() => {
    const handleCallback = async () => {
      try {
        // Check for error from OAuth provider
        const error = searchParams.get('error');
        if (error) {
          throw new Error(`Authentication failed: ${error}`);
        }

        // Get the token from the URL if it's there
        const token = searchParams.get('token');
        
        if (token) {
          // Store the token
          localStorage.setItem('token', token);
          
          // Fetch user data
          const response = await fetch(`${import.meta.env.REACT_APP_API_BASE_URL}/api/auth/me`, {
            headers: {
              'Authorization': `Bearer ${token}`
            },
            credentials: 'include'
          });
          
          if (response.ok) {
            const userData = await response.json();
            login(userData, token);
            navigate('/dashboard');
          } else {
            throw new Error('Failed to fetch user data');
          }
        } else {
          // If no token, try to get user data directly (in case using cookies)
          try {
            const response = await fetch(`${import.meta.env.REACT_APP_API_BASE_URL}/api/auth/me`, {
              credentials: 'include'
            });
            
            if (response.ok) {
              const userData = await response.json();
              login(userData, localStorage.getItem('token') || '');
              navigate('/dashboard');
              return;
            }
          } catch (err) {
            console.error('Error fetching user data:', err);
          }
          
          // If we get here, authentication failed
          navigate('/login?error=auth_failed');
        }
      } catch (error) {
        console.error('Authentication error:', error);
        navigate(`/login?error=${encodeURIComponent(error.message)}`);
      }
    };

    handleCallback();
  }, [login, navigate, searchParams]);

  return <div>Completing authentication...</div>;
};

export default AuthCallback;