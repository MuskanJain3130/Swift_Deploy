import 'bootstrap/dist/css/bootstrap.min.css';
import './App.css';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import NotFound from './Pages/NotFound';
import  Logs from './Pages/Logs';
import {NavigationBar} from './Components/NavigationBar';
import Footer from './Components/Footer';
import Header from './Components/Header';
import Landing from './Pages/Landing';
import Profile from './Pages/Profile';
import Projects from './Pages/Projects';
import AuthCallback from './Components/AuthCallback';
import CompleteProfile from './Pages/CompleteProfile';
import Login from './Pages/Login';
import RegisterUser from './Pages/RegisterUser';
import { useNavigate } from 'react-router-dom';
import NetlifyCallback from './Components/NetlifyCallback';
 
function App() {
  const AuthCallbackWrapper = () => {
  const navigate = useNavigate();
  return <AuthCallback navigate={navigate} />;
};
  return (
    <div className="App">
      <Router>
        <Routes>
          <Route path="/" element={<NavigationBar />} />
          <Route path="/*" element={<NotFound />} />
          <Route path="/footer" element={<Footer />} />
          <Route path="/header" element={<Header />} />
          <Route path="/landing" element={<Landing />} />
          <Route path="/auth-callback" element={<AuthCallbackWrapper />} />
          <Route path="/netlify-callback" element={<NetlifyCallback />} />
          <Route path="/logs" element={<Logs a="" b={0}/>} />
          <Route path="/projects" element={<Projects />} />
          <Route path="/complete-profile/:userId" element={<CompleteProfile />} />
          <Route path="/register" element={<RegisterUser />} />
          <Route path="/profile" element={<Profile />} />
          <Route path="/login" element={<Login />} />


        </Routes>
      </Router>
    </div>
  );
}

export default App;
