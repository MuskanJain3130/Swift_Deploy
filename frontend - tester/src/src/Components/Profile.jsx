// src/Components/Profile.jsx
import React, { useState } from 'react';
import { FaGithub,FaEnvelope } from 'react-icons/fa';
import { useAuth } from '../Contexts/AuthContext';
import '../css/Profile.css';

const Profile = () => {
  const { user, logout } = useAuth();
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [errors, setErrors] = useState({});
  const [success, setSuccess] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSetPassword = async (e) => {
    e.preventDefault();
    setErrors({});
    setSuccess('');

    if (!password) {
      setErrors({ password: 'Password is required' });
      return;
    }

    if (password.length < 6) {
      setErrors({ password: 'Password must be at least 6 characters' });
      return;
    }

    if (password !== confirmPassword) {
      setErrors({ confirmPassword: 'Passwords do not match' });
      return;
    }

    setIsLoading(true);

    try {
      const response = await fetch(`${import.meta.env.REACT_APP_API_BASE_URL}/api/auth/set-password`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        credentials: 'include',
        body: JSON.stringify({ newPassword: password })
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.message || 'Failed to set password');
      }

      setSuccess('Password set successfully');
      setPassword('');
      setConfirmPassword('');
    } catch (error) {
      setErrors({ submit: error.message });
    } finally {
      setIsLoading(false);
    }
  };
  const handleGithubConnect = async () => {
    window.location.href = `${import.meta.env.VITE_API_BASE_URL}/api/auth/github/login`;
  }
  return (
    <div className="profile-container">
      <h2>Profile Settings</h2>
      
      <div className="profile-section">
        <h3>Account Information</h3>
        <p><strong>Name:</strong> {user?.name}</p>
        <p><strong>Email:</strong> {user?.email}</p>
      </div>

      <div className="profile-section">
        <h3>Authentication Methods</h3>
        
        <div className="auth-method">
          <div className="method-header">
            <FaEnvelope className="method-icon" />
            <span>Email & Password</span>
            {user?.hasPassword ? (
              <span className="badge success">Active</span>
            ) : (
              <span className="badge warning">Not Set</span>
            )}
          </div>
          
          {!user?.hasPassword && (
            <div className="set-password-form">
              <h4>Set a Password</h4>
              <form onSubmit={handleSetPassword}>
                <div className="form-group">
                  <label>New Password</label>
                  <input
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className={errors.password ? 'error' : ''}
                  />
                  {errors.password && (
                    <span className="error-text">{errors.password}</span>
                  )}
                </div>
                
                <div className="form-group">
                  <label>Confirm Password</label>
                  <input
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    className={errors.confirmPassword ? 'error' : ''}
                  />
                  {errors.confirmPassword && (
                    <span className="error-text">{errors.confirmPassword}</span>
                  )}
                </div>
                
                <button 
                  type="submit" 
                  className="btn primary"
                  disabled={isLoading}
                >
                  {isLoading ? 'Saving...' : 'Set Password'}
                </button>
                
                {errors.submit && (
                  <div className="error-message">{errors.submit}</div>
                )}
                
                {success && (
                  <div className="success-message">{success}</div>
                )}
              </form>
            </div>
          )}
        </div>

        <div className="auth-method">
          <div className="method-header">
            <FaGithub className="method-icon" />
            <span>GitHub</span>
            {user?.hasGithub ? (
              <span className="badge success">Connected</span>
            ) : (
              <button 
                className="btn secondary"
                onClick={handleGithubConnect}
                disabled={isLoading}
              >
                Connect GitHub
              </button>
            )}
          </div>
        </div>
      </div>

      <div className="profile-actions">
        <button className="btn danger" onClick={logout}>
          Sign Out
        </button>
      </div>
    </div>
  );
};

export default Profile;