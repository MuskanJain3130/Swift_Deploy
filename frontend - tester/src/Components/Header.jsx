import React from "react";
import "../css/Header.css";
import { FaGithub } from "react-icons/fa";
import { useNavigate } from "react-router-dom";

const Header = () => {
  const navigate= useNavigate();
  const handleLogin=()=>{
    window.location.href = "http://localhost:5280/api/auth/github/login";
    // window.location.href = "http://localhost:5280/api/auth/netlify/login";
  }
  return (
    <header className="header">
      <div className="header-left">
        <img src={"/logo.ico"} alt="SwiftDeploy Logo" className="logo" />
        <span className="title">SwiftDeploy</span>
      </div>
      <div className="header-right">
        <button className="github-login" onClick={handleLogin}>
          <FaGithub className="github-icon" />
          Login with GitHub
        </button>
      </div>
    </header>
  );
};

export default Header;
