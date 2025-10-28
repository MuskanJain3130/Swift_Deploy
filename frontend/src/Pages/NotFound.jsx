import React from "react";
import "../css/notfound.css";
import {useNavigate} from "react-router-dom";
function NotFound() {
  const navigate = useNavigate();
  const handleHome = () => {navigate("/");};
  return (
    <div className="background d-flex justify-content-center align-items-center w-100 h-100 px-5">
      <div className="overlay-border rounded-3 w-50 h-50 p-5">
        <span className="display-1 fw-bold text-center text-white" >
          404
        </span>
        <h2 className="fw-bold text-white text-center fs-2">
          Page Not Found
        </h2>
        <span className="fs-6 text-white mb-4">
          Sorry, we couldn't find the page you’re looking for.
        </span>
        <button
          className="btn btn-sm
          btn-purple text-white fw-bold"
          onClick={handleHome}
        >
          Go Home
        </button>
      </div>
    </div>
  );
}

export default NotFound;
