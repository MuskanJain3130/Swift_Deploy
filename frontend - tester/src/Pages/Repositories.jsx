// src/components/Repositories.jsx
import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

const Repositories = () => {
  const [repos, setRepos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const navigate = useNavigate();

  useEffect(() => {
    const fetchRepositories = async () => {
      const token = localStorage.getItem('github_access_token');
      if (!token) {
        // If there's no token, redirect to the login page
        navigate('/');
        return;
      }
      const handleRepoClick = (owner="tamannashah18", repoName) => {
        // Navigate to the repository content page with owner and repoName as parameters
        navigate(`/repositories/${owner}/${repoName}`);
      };

      try {
        // Make the API call to your .NET Core backend using fetch
        const response = await fetch('http://localhost:5280/api/repositories', {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`// Include the Bearer token in the Authorization header
          }
        });

        if (!response.ok) {
          // If the response is not OK (e.g., 401 Unauthorized), throw an error
          const errorText = await response.text();
          if (response.status === 401) {
            throw new Error('Unauthorized: Session expired. Please log in again.');
          }
          throw new Error(`Failed to fetch repositories: ${response.status} ${errorText}`);
        }

        const data = await response.json();
        setRepos(data);
      } catch (err) {
        console.error('Failed to fetch repositories:', err);
        setError(err.message);
        
        // If the error indicates an expired token, clear it and redirect
        if (err.message.includes('Unauthorized')) {
          localStorage.removeItem('github_access_token');
          navigate('/');
        }
      } finally {
        setLoading(false);
      }
    };

    fetchRepositories();
  }, [navigate]);

  if (loading) {
    return <div>Loading repositories...</div>;
  }

  if (error) {
    return <div style={{ color: 'red' }}>Error: {error}</div>;
  }

  return (
    <div style={{ padding: '20px' }}>
      <h1>Your GitHub Repositories</h1>
      {repos.length > 0 ? (
        <ul>
          {repos.map(repo => (
            <li key={repo.id} style={{ marginBottom: '10px' }}>
              <a href={repo.htmlUrl} target="_blank" rel="noopener noreferrer">
                <strong>{repo.name}</strong>
              </a>
              <p>{repo.description}</p>
            </li>
          ))}
        </ul>
      ) : (
        <p>You don't have any repositories.</p>
      )}
    </div>
  );
};

export default Repositories;
