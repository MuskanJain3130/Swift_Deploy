import React, { useState, useEffect } from "react";
import axios from "axios";

function Profile() {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState("");
  const [formData, setFormData] = useState({
    username: "",
    name: "",
    email: ""
  });
  const [editMode, setEditMode] = useState(false);
  const [message, setMessage] = useState("");

  // Load token and user from localStorage on mount
  useEffect(() => {
    const storedToken = localStorage.getItem("token");
    const storedUser = localStorage.getItem("user");
    if (storedToken && storedUser) {
      const parsedUser = JSON.parse(storedUser);
      setToken(storedToken);
      setUser(parsedUser);
      setFormData({
        username: parsedUser.username || "",
        name: parsedUser.name || "",
        email: parsedUser.email || ""
      });
    }
  }, []);

  const handleChange = (e) => {
    setFormData((prev) => ({
      ...prev,
      [e.target.name]: e.target.value
    }));
  };

  const handleUpdate = async (e) => {
    e.preventDefault();
    setMessage("");
    if (!user || !token) {
      setMessage("User not logged in.");
      return;
    }

    try {
      const response = await axios.put(
        `http://localhost:5280/api/user/${user.id}`,
        {
          Username: formData.username,
          Name: formData.name,
          Email: formData.email
        },
        {
          headers: {
            Authorization: `Bearer ${token}`,
            "Content-Type": "application/json"
          }
        }
      );

      // Update local state and localStorage
      const updatedUser = {
        ...user,
        username: formData.username,
        name: formData.name,
        email: formData.email
      };
      setUser(updatedUser);
      localStorage.setItem("user", JSON.stringify(updatedUser));

      setMessage("Profile updated successfully");
      setEditMode(false);
    } catch (err) {
      setMessage(err.response?.data?.message || "Failed to update profile");
    }
  };

  if (!user) {
    return <div className="page-container"><div className="card">Loading profile or not logged in.</div></div>;
  }

  return (
    <div className="page-container">
      <div className="card">
        {/* <img
          src={user.avatarUrl || "/logo.png"}
          alt="Avatar"
          className="logo"
        /> */}
        <h2 className="page-title">Your Profile</h2>

        {editMode ? (
          <form onSubmit={handleUpdate}>
            <input
              type="text"
              name="username"
              value={formData.username}
              onChange={handleChange}
              placeholder="Username"
              minLength={3}
              maxLength={50}
              required
            />
            <input
              type="text"
              name="name"
              value={formData.name}
              onChange={handleChange}
              placeholder="Name"
              required
            />
            <input
              type="email"
              name="email"
              value={formData.email}
              onChange={handleChange}
              placeholder="Email"
              required
            />
            <button type="submit" className="btn">
              Save
            </button>
            <button
              type="button"
              onClick={() => setEditMode(false)}
              className="btn"
              style={{ marginTop: "10px", background: "#ccc", color: "#000" }}
            >
              Cancel
            </button>
          </form>
        ) : (
          <div style={{ width: "100%", textAlign: "left" }}>
            <p>
              <strong>Username:</strong> {user.username}
            </p>
            <p>
              <strong>Name:</strong> {user.name}
            </p>
            <p>
              <strong>Email:</strong> {user.email}
            </p>
            <button onClick={() => setEditMode(true)} className="btn">
              Edit Profile
            </button>
          </div>
        )}
        {message && (
          <div
            className={`message ${
              message.toLowerCase().includes("failed") ? "error" : "success"
            }`}
          >
            {message}
          </div>
        )}
      </div>
    </div>
  );
}

export default Profile;
