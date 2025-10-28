import 'bootstrap/dist/css/bootstrap.min.css';
import './App.css';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import NotFound from './Pages/NotFound';
import  Logs from './Pages/Logs';
import {NavigationBar} from './Components/NavigationBar';
import Footer from './Components/Footer';
import Header from './Components/Header';
import Landing from './Pages/Landing';
import AuthCallback from './Components/AuthCallback';
import Authtest from './Pages/Authtest';
import Repositories from './Pages/Repositories';
import RepoContent from './Pages/RepoContent';
import { useNavigate } from 'react-router-dom'; 
import Deployments from './Pages/Deployments';
import NetlifyCallback from './Components/NetlifyCallback';
const AuthCallbackWrapper = () => {
  const navigate = useNavigate();
  return <AuthCallback navigate={navigate} />;
};

function App() {
  return (
    <div className="App">
      <Router>
        <Routes>
          <Route path="/" element={<NavigationBar />} />
          <Route path="/notfound" element={<NotFound />} />
          <Route path="/footer" element={<Footer />} />
          <Route path="/header" element={<Header />} />
          <Route path="/landing" element={<Landing />} />
          <Route path="/test" element={<Authtest />} />
          <Route path="/auth-callback" element={<AuthCallbackWrapper />} />
          <Route path="/netlify-callback" element={<NetlifyCallback />} />
          
          <Route path="/repos" element={<Repositories />} />
          
          <Route path="/deployments" element={<Deployments />} />
          <Route path="/repoContent/:owner/:repoName" element={<RepoContent />} />
          <Route path="/logs" element={<Logs />} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </Router>
    </div>
  );
}

export default App;
