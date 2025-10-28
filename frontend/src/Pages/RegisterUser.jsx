import React, { useState } from "react";
import axios from "axios";
import '../css/Forms.css';
import { useNavigate } from "react-router-dom";

function RegisterUser() {
    const navigate = useNavigate();
  const [formData, setFormData] = useState({
    username: "",
    email: "",
    password: "",
    name: "",
    confirmPassword: ""
  });

  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const handleChange = (e) => {
    setFormData((prev) => ({
      ...prev,
      [e.target.name]: e.target.value
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    if (
      formData.username.length < 3 ||
      formData.username.length > 50 ||
      !formData.email.match(/^\S+@\S+\.\S+$/) ||
      formData.password.length < 6 ||
      formData.password.length > 100 ||
      formData.name.trim() === ""
    ) {
      setError("Please fill the form correctly.");
      return;
    }
    if (formData.password !== formData.confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    try {
      await axios.post(
        "http://localhost:5280/api/user/register",
        {
          Username: formData.username,
          Email: formData.email,
          Password: formData.password,
          Name: formData.name
        },
        { headers: { "Content-Type": "application/json" } }
      ).then((response) => { 
        setFormData({ username: "", email: "", password: "", name: "", confirmPassword: "" });
        navigate("/login");
     }).catch((error) => { console.log(error); });
    } catch (err) {
      setError(err.response?.data?.message || "Registration failed");
    }
  };

  return (
    <div className="page-container">
      <div className="card">
        <div className="d-flex justify-content-between align-items-center">
        <img src="/FullLogo.png" alt="Logo" className="logo"/>
        <span className="page-title">Register</span>
        </div>
        <form onSubmit={handleSubmit}>
          <input
            type="text"
            name="username"
            placeholder="Username"
            value={formData.username}
            onChange={handleChange}
            minLength={3}
            maxLength={50}
            required
          />
          <input
            type="text"
            name="name"
            placeholder="Name"
            value={formData.name}
            onChange={handleChange}
            required
          />
          <input
            type="email"
            name="email"
            placeholder="Email"
            value={formData.email}
            onChange={handleChange}
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
          <input
            type="password"
            name="confirmPassword"
            placeholder="Confirm Password"
            value={formData.confirmPassword}
            onChange={handleChange}
            required
          />
          <button type="submit" className="btn">
            Register
          </button>
        </form>
        <span>Registered Already? <a href="/login">Login</a></span>
        {error && <div className="message error">{error}</div>}
        {success && <div className="message success">{success}</div>}
      </div>
    </div>
  );
}

export default RegisterUser;
