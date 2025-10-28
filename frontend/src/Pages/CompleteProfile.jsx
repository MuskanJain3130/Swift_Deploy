import React, { useEffect, useState } from "react";
import axios from "axios";
import '../css/Forms.css';
import {useParams,useNavigate} from 'react-router-dom';
function CompleteProfile() {
    const params=useParams();
    const navigate=useNavigate();
    const[userId,setUserId]=useState("");
    const [formData, setFormData] = useState({
        username: "",
        password: "",
        confirmPassword: ""
    });
    useEffect(() => {
        if (params.userId) {
            setUserId(params.userId);
        }
        if(params.userId==""){
            setError("Invalid user id");
        }
        console.log(userId);
    },[]);

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
      formData.username.length > 50
    ) {
      setError("Username must be between 3 and 50 characters.");
      return;
    }
    if (formData.password.length < 6 || formData.password.length > 100) {
      setError("Password must be between 6 and 100 characters.");
      return;
    }
    if (formData.password !== formData.confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    try {
      await axios.post(
        "http://localhost:5280/api/User/complete-profile",
        {
          Username: formData.username,
          Password: formData.password,
          UserId: userId
        },
        { headers: { "Content-Type": "application/json" } }
      ).then((response) => navigate("/login")).catch((error) => console.log(error));
    } catch (err) {
      setError(err.response?.data?.message || "Profile completion failed");
    }
  };

  return (
    <div className="page-container">
      <div className="card">
        {/* Example logo */}
        <div className="d-flex justify-content-between align-items-center">
        <img src="/FullLogo.png" alt="Logo" className="logo"/>
        <span className="page-title">Complete Your Profile</span>
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
            Complete Profile
          </button>
        </form>
        {error && <div className="message error">{error}</div>}
        {success && <div className="message success">{success}</div>}
      </div>
    </div>
  );
}

export default CompleteProfile;
