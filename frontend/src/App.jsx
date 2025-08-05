import 'bootstrap/dist/css/bootstrap.min.css';
import './App.css';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import NotFound from './Pages/NotFound';
import {NavigationBar} from './Components/NavigationBar';
 

function App() {
  return (
    <div className="App">
      <Router>
        <Routes>
          <Route path="/" element={<NavigationBar />} />
          <Route path="/notfound" element={<NotFound />} />
        </Routes>
      </Router>
    </div>
  );
}

export default App;
