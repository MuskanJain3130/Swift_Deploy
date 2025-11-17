// src/services/api.js
import axios from 'axios';

const API_URL = 'http://localhost:5280/api'; // Update with your backend URL

const api = axios.create({
  baseURL: API_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Add a request interceptor to include the auth token
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Projects
export const getProjects = async () => {
  // Simulate API call
  return new Promise(resolve => {
    setTimeout(() => {
      resolve([
        {
          id: '1',
          name: 'My First Project',
          description: 'A sample project to get started with SwiftDeploy',
          status: 'active',
          deploymentPlatforms: ['github', 'netlify'],
          updatedAt: new Date().toISOString()
        },
        {
          id: '2',
          name: 'E-commerce Store',
          description: 'A full-featured online store built with React and Node.js',
          status: 'active',
          deploymentPlatforms: ['vercel', 'cloudflare'],
          updatedAt: new Date(Date.now() - 86400000).toISOString() // Yesterday
        }
      ]);
    }, 500);
  });
};

export const getProject = async (id) => {
  // Simulate API call
  return new Promise(resolve => {
    setTimeout(() => {
      resolve({
        id,
        name: id === '1' ? 'My First Project' : 'E-commerce Store',
        description: id === '1' 
          ? 'A sample project to get started with SwiftDeploy' 
          : 'A full-featured online store built with React and Node.js',
        status: 'active',
        deploymentPlatforms: id === '1' ? ['github', 'netlify'] : ['vercel', 'cloudflare'],
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      });
    }, 500);
  });
};

// Deployments
export const getDeploymentStatus = async (projectId) => {
  // Simulate API call
  return new Promise(resolve => {
    setTimeout(() => {
      resolve({
        github: {
          state: 'success',
          url: `https://github.com/username/project-${projectId}`,
          lastDeployed: new Date(Date.now() - 3600000).toISOString() // 1 hour ago
        },
        netlify: {
          state: projectId === '1' ? 'success' : 'pending',
          url: projectId === '1' ? `https://project-${projectId}.netlify.app` : null,
          lastDeployed: projectId === '1' ? new Date().toISOString() : null
        },
        vercel: {
          state: projectId === '2' ? 'success' : 'pending',
          url: projectId === '2' ? `https://project-${projectId}.vercel.app` : null,
          lastDeployed: projectId === '2' ? new Date().toISOString() : null
        },
        cloudflare: {
          state: projectId === '2' ? 'error' : 'pending',
          error: projectId === '2' ? 'Build failed: Missing configuration' : null,
          lastDeployed: projectId === '2' ? new Date().toISOString() : null
        }
      });
    }, 500);
  });
};

export default api;