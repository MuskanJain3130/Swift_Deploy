import React from "react";
import "../css/Header.css";
import { FaGithub } from "react-icons/fa";
import { useNavigate } from "react-router-dom";

const Header = () => {
  const navigate= useNavigate();
  const handleLogin=(type)=>{
    if(type==="normal"){
      navigate('/login');
    }else{
    window.location.href = "http://localhost:5280/api/auth/github/login";
    }
  }
  return (
    <header className="header">
      <div className="header-left">
        <img src={"/logo.ico"} alt="SwiftDeploy Logo" className="logo mt-3 p-3" />
        <span className="title">SwiftDeploy</span>
      </div>
      <div className="header-right">
        <button className="github-login mx-2" onClick={()=>handleLogin("github")}>
          <FaGithub className="github-icon" />
          Login with GitHub
        </button>
        <button className="github-login mx-2" onClick={()=>handleLogin("normal")}>
          Login
        </button>
      </div>
    </header>
  );
};

export default Header;
