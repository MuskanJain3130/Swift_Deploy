// src/Components/LoginModal.jsx
import React, { useState } from 'react';
import { FaGithub, FaTimes, FaEnvelope, FaLock } from 'react-icons/fa';
import { useAuth } from '../Contexts/AuthContext';
import '../css/LoginModal.css';

const LoginModal = ({ isOpen, onClose }) => {
  const [isLogin, setIsLogin] = useState(true);
  const [formData, setFormData] = useState({
    name: '',
    email: '',
    password: '',
    confirmPassword: ''
  });
  const [errors, setErrors] = useState({});
  const [isLoading, setIsLoading] = useState(false);
  const { login } = useAuth();

  if (!isOpen) return null;

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
  };

  const validateForm = () => {
    const newErrors = {};
    
    if (!isLogin && !formData.name.trim()) {
      newErrors.name = 'Name is required';
    }
    
    if (!formData.email) {
      newErrors.email = 'Email is required';
    } else if (!/\S+@\S+\.\S+/.test(formData.email)) {
      newErrors.email = 'Email is invalid';
    }
    
    if (!formData.password) {
      newErrors.password = 'Password is required';
    } else if (formData.password.length < 6) {
      newErrors.password = 'Password must be at least 6 characters';
    }
    
    if (!isLogin && formData.password !== formData.confirmPassword) {
      newErrors.confirmPassword = 'Passwords do not match';
    }
    
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!validateForm()) return;
    
    setIsLoading(true);
    setErrors({});
    
    try {
      const url = `${import.meta.env.REACT_APP_API_BASE_URL}/api/auth/${isLogin ? 'login' : 'register'}`;
      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify(isLogin 
          ? { 
              email: formData.email, 
              password: formData.password 
            }
          : { 
              name: formData.name, 
              email: formData.email, 
              password: formData.password 
            }
        ),
      });
      
      const data = await response.json();
      
      if (!response.ok) {
        throw new Error(data.message || 'Something went wrong');
      }
      
      // Login successful
      login(data.user, data.token);
      onClose();
    } catch (error) {
      setErrors({ submit: error.message });
    } finally {
      setIsLoading(false);
    }
  };

  const handleGithubLogin = async () => {
    try {
      // First, get the GitHub auth URL from the backend
      const response = await fetch(`${import.meta.env.REACT_APP_API_BASE_URL}/api/auth/github`, {
        credentials: 'include'
      });
      
      if (!response.ok) {
        throw new Error('Failed to initiate GitHub login');
      }
      
      const { url } = await response.json();
      
      // Redirect to GitHub for authentication
      window.location.href = url;
    } catch (error) {
      setErrors({ submit: error.message });
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <button className="close-btn" onClick={onClose}>
          <FaTimes />
        </button>
        
        <div className="modal-header">
          <h2>{isLogin ? 'Welcome Back' : 'Create Account'}</h2>
          <p>{isLogin ? 'Sign in to continue' : 'Get started with your account'}</p>
        </div>
        
        {errors.submit && (
          <div className="error-message">{errors.submit}</div>
        )}
        
        <form onSubmit={handleSubmit} className="auth-form">
          {!isLogin && (
            <div className="form-group">
              <div className="input-icon">
                <FaEnvelope className="icon" />
              </div>
              <input
                type="text"
                name="name"
                placeholder="Full Name"
                value={formData.name}
                onChange={handleChange}
                className={errors.name ? 'error' : ''}
              />
              {errors.name && <span className="error-text">{errors.name}</span>}
            </div>
          )}
          
          <div className="form-group">
            <div className="input-icon">
              <FaEnvelope className="icon" />
            </div>
            <input
              type="email"
              name="email"
              placeholder="Email"
              value={formData.email}
              onChange={handleChange}
              className={errors.email ? 'error' : ''}
            />
            {errors.email && <span className="error-text">{errors.email}</span>}
          </div>
          
          <div className="form-group">
            <div className="input-icon">
              <FaLock className="icon" />
            </div>
            <input
              type="password"
              name="password"
              placeholder="Password"
              value={formData.password}
              onChange={handleChange}
              className={errors.password ? 'error' : ''}
            />
            {errors.password && <span className="error-text">{errors.password}</span>}
          </div>
          
          {!isLogin && (
            <div className="form-group">
              <div className="input-icon">
                <FaLock className="icon" />
              </div>
              <input
                type="password"
                name="confirmPassword"
                placeholder="Confirm Password"
                value={formData.confirmPassword}
                onChange={handleChange}
                className={errors.confirmPassword ? 'error' : ''}
              />
              {errors.confirmPassword && (
                <span className="error-text">{errors.confirmPassword}</span>
              )}
            </div>
          )}
          
          <button 
            type="submit" 
            className="auth-button primary"
            disabled={isLoading}
          >
            {isLoading ? 'Processing...' : isLogin ? 'Sign In' : 'Sign Up'}
          </button>
        </form>
        
        <div className="divider">
          <span>OR</span>
        </div>
        
        <button 
          className="auth-button github"
          onClick={handleGithubLogin}
          disabled={isLoading}
        >
          <FaGithub className="icon" />
          Continue with GitHub
        </button>
        
        <div className="auth-footer">
          {isLogin ? (
            <p>
              Don't have an account?{' '}
              <button 
                type="button" 
                className="text-button"
                onClick={() => setIsLogin(false)}
              >
                Sign up
              </button>
            </p>
          ) : (
            <p>
              Already have an account?{' '}
              <button 
                type="button" 
                className="text-button"
                onClick={() => setIsLogin(true)}
              >
                Sign in
              </button>
            </p>
          )}
        </div>
      </div>
    </div>
  );
};

export default LoginModal;