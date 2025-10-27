import React, { useState } from "react";
import axios from "axios";
import '../css/Forms.css';
import { useNavigate } from "react-router-dom";

function Login() {
  const [formData, setFormData] = useState({
    UsernameOrEmail: "",
    password: ""
  });
  const navigate = useNavigate();
  const [error, setError] = useState("");

  const handleChange = (e) => {
    setFormData((prev) => ({
      ...prev,
      [e.target.name]: e.target.value
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    if (
      formData.UsernameOrEmail.length < 3 ||
      formData.UsernameOrEmail.length > 50 ||
      formData.password.length < 6 ||
      formData.password.length > 100
    ) {
      setError("Please fill the form correctly.");
      return;
    }
    try {
      await axios.post(
        "http://localhost:5280/api/User/login",
        {
          UsernameOrEmail: formData.UsernameOrEmail,
          Password: formData.password
        },
        { headers: { "Content-Type": "application/json" } }
      ).then((response) => 
        {
        localStorage.setItem("token", response.data.token);
        localStorage.setItem("user",JSON.stringify( response.data.user));
        navigate("/projects");
    });
      setFormData({ UsernameOrEmail: "", password: "" });
    } catch (err) {
      setError(err.response?.data?.message || "Login failed");
    }
  };

  return (
    <div className="page-container">
      <div className="card">
        <div className="d-flex justify-content-between align-items-center">
        <img src="/FullLogo.png" alt="Logo" className="logo"/>
        <span className="page-title">Login</span>
        </div>
        <form onSubmit={handleSubmit}>
          <input
            type="text"
            name="UsernameOrEmail"
            placeholder="UsernameOrEmail"
            value={formData.UsernameOrEmail}
            onChange={handleChange}
            minLength={3}
            maxLength={50}
            required
          />
          <input
            type="password"
            name="password"
            placeholder="Password"
            value={formData.password}
            onChange={handleChange}
            minLength={6}
            maxLength={100}
            required
          />
          <button type="submit" className="btn">
            Login
          </button>
          <div className="message">
            Don't have an account? <a href="/register">Register</a>
          </div>
        </form>
        {error && <div className="message error">{error}</div>}
      </div>
    </div>
  );
}

export default Login;
