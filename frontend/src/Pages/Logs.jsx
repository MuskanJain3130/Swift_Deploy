import React, { useState } from 'react';
import PropTypes from 'prop-types';
import { ChevronDown, ChevronRight, ArrowLeft } from 'lucide-react';
import NavigationBar from '../Components/NavigationBar'; // Assuming this path is correct for your NavigationBar
import '../css/Logs.css'; // Ensure this path is correct relative to Logs.js

const Logs = () => {
  const [buildLogsOpen, setBuildLogsOpen] = useState(true);
  const [deploymentLogsOpen, setDeploymentLogsOpen] = useState(true);
  const [postDeploymentOpen, setPostDeploymentOpen] = useState(true);

  const buildLogs = [
    { type: 'INFO', message: 'Initializing build process...' },
    { type: 'INFO', message: 'Fetching repository: my-awesome-project' },
    { type: 'INFO', message: 'Installing dependencies (npm)...' },
    { type: 'SUCCESS', message: 'Dependencies installed.' },
    { type: 'INFO', message: 'Running build script...' },
    { type: 'WARN', message: 'ESLint warnings detected, continuing build.' },
    { type: 'SUCCESS', message: 'Build completed successfully.' }
  ];

  const deploymentLogs = [
    { type: 'INFO', message: 'Preparing deployment to Vercel...' },
    { type: 'INFO', message: 'Uploading build artifacts...' },
    { type: 'SUCCESS', message: 'Artifacts uploaded.' },
    { type: 'INFO', message: 'Assigning domain...' },
    { type: 'SUCCESS', message: 'Deployment successful' },
    { type: 'INFO', message: 'Application available at: https://my-awesome-project.vercel.app', link: true }
  ];

  const postDeploymentLogs = [
    { type: 'INFO', message: 'Running post-deployment tests...' },
    { type: 'SUCCESS', message: 'All tests passed.' },
    { type: 'INFO', message: 'Notifying team via Slack...' },
    { type: 'SUCCESS', message: 'Notifications sent.' }
  ];

  const LogEntryComponent = ({ log }) => (
    <div className={`log-entry log-${log.type?.toLowerCase()}`}>
      <span className="log-type">[{log.type}]</span>
      <span className={log.link ? 'log-link' : 'log-message'}>
        {log.link ? (
          <a href={log.message} target="_blank" rel="noopener noreferrer">
            {log.message}
          </a>
        ) : (
          log.message
        )}
      </span>
    </div>
  );

  return (
    <div className="logs">
      {/* NavigationBar should be a direct child of .logs, before the main content */}
      <NavigationBar />
      <div className="background-2">
        <div className="background-4">
          {/* The content that appears on top of the gradient background */}
          <div className="deployment-container">
            <div className="deployment-header">
              <div className="project-info">
                <ArrowLeft className="back-arrow" size={20} />
                <h1 className="project-name">my-awesome-project</h1>
                <span className="react-tag">REACT</span>
              </div>
              <div className="deployment-status">
                <div className="status-badge success">✓ DEPLOYMENT SUCCESSFUL</div>
                <div className="action-buttons">
                  <button className="retry-btn">Retry Deployment</button>
                  <button className="dashboard-btn">View in Dashboard</button>
                </div>
              </div>
            </div>

            <div className="logs-container visible">
              {/* BUILD LOGS */}
              <div className="log-section">
                <div className="log-section-header" onClick={() => setBuildLogsOpen(!buildLogsOpen)}>
                  {buildLogsOpen ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  <span>BUILD LOGS</span>
                </div>
                {buildLogsOpen && (
                  <div className="log-entries">
                    {buildLogs.map((log, index) => (
                      <LogEntryComponent key={index} log={log} />
                    ))}
                  </div>
                )}
              </div>

              {/* DEPLOYMENT LOGS */}
              <div className="log-section">
                <div className="log-section-header" onClick={() => setDeploymentLogsOpen(!deploymentLogsOpen)}>
                  {deploymentLogsOpen ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  <span>DEPLOYMENT LOGS</span>
                </div>
                {deploymentLogsOpen && (
                  <div className="log-entries">
                    {deploymentLogs.map((log, index) => (
                      <LogEntryComponent key={index} log={log} />
                    ))}
                  </div>
                )}
              </div>

              {/* POST DEPLOYMENT LOGS */}
              <div className="log-section">
                <div className="log-section-header" onClick={() => setPostDeploymentOpen(!postDeploymentOpen)}>
                  {postDeploymentOpen ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  <span>POST-DEPLOYMENT HOOKS</span>
                </div>
                {postDeploymentOpen && (
                  <div className="log-entries">
                    {postDeploymentLogs.map((log, index) => (
                      <LogEntryComponent key={index} log={log} />
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};


export default Logs;
