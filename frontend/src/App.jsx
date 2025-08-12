import 'bootstrap/dist/css/bootstrap.min.css';
import './App.css';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import NotFound from './Pages/NotFound';
import  Logs from './Pages/Logs';
import {NavigationBar} from './Components/NavigationBar';
import Footer from './Components/Footer';
import Header from './Components/Header';
import Landing from './Pages/Landing';
import Projects from './Pages/Projects';
 

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
          <Route path="/logs" element={<Logs />} />
          <Route path="/projects" element={<Projects />} />

        </Routes>
      </Router>
    </div>
  );
}

export default App;
