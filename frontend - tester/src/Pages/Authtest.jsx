import React from 'react'

function Authtest() {
//   const handleLogin = async () => {
//     try {
//       const response = await fetch('http://localhost:5280/api/Auth/github/login', {
//         method: 'GET',
//       });
//       const data = await response.json();
//       console.log(data);
//       // handle login response here (e.g., redirect, show message)
//     } catch (error) {
//       console.error('Login failed:', error);
//     }
//   };
const handleLogin = () => {
    window.location.href = "http://localhost:5280/api/auth/github/login";
  };
  return (
    <div>
      <button onClick={handleLogin}>Login</button>
    </div>
  )
}

export default Authtest;
