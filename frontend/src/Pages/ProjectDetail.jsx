// src/pages/ProjectDetail.jsx
import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { 
  Container, 
  Row, 
  Col, 
  Card, 
  Button, 
  Spinner, 
  Alert, 
  Nav, 
  Tab, 
  Badge
} from 'react-bootstrap';
import { ArrowLeft, Gear } from 'react-bootstrap-icons';
import { getProject, getDeploymentStatus } from '../Services/api';
import DeploymentPlatform from '../Components/DeploymentPlatform';

const ProjectDetail = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const [project, setProject] = useState(null);
  const [deploymentStatus, setDeploymentStatus] = useState({});
  const [loading, setLoading] = useState(true);
  const [deploying, setDeploying] = useState(false);
  const [activeTab, setActiveTab] = useState('deployments');
  const [error, setError] = useState('');

  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoading(true);
        const [projectData, deploymentData] = await Promise.all([
          getProject(id),
          getDeploymentStatus(id)
        ]);
        setProject(projectData);
        setDeploymentStatus(deploymentData);
      } catch (err) {
        setError('Failed to load project details');
        console.error('Error fetching project details:', err);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, [id]);

  const handleDeploy = async (platform) => {
    try {
      setDeploying(true);
      // Simulate deployment
      console.log(`Deploying to ${platform}...`);
      await new Promise(resolve => setTimeout(resolve, 2000));
      
      // Update deployment status
      setDeploymentStatus(prev => ({
        ...prev,
        [platform]: { 
          state: 'success', 
          url: `https://${platform}.example.com/${id}`,
          lastDeployed: new Date().toISOString()
        }
      }));
    } catch (err) {
      console.error(`Error deploying to ${platform}:`, err);
      setError(`Failed to deploy to ${platform}`);
    } finally {
      setDeploying(false);
    }
  };

  if (loading && !project) {
    return (
      <div className="d-flex justify-content-center align-items-center" style={{ minHeight: '60vh' }}>
        <Spinner animation="border" role="status">
          <span className="visually-hidden">Loading...</span>
        </Spinner>
      </div>
    );
  }

  if (!project) {
    return (
      <Container className="py-5 text-center">
        <Alert variant="danger">Project not found</Alert>
        <Button variant="outline-primary" onClick={() => navigate('/projects')} className="mt-3">
          <ArrowLeft className="me-2" /> Back to Projects
        </Button>
      </Container>
    );
  }

  return (
    <Container className="py-4">
      <div className="d-flex align-items-center mb-4">
        <Button 
          variant="outline-secondary" 
          size="sm" 
          className="me-3"
          onClick={() => navigate('/projects')}
        >
          <ArrowLeft /> Back
        </Button>
        <div>
          <h1 className="h3 mb-0">{project.name}</h1>
          <p className="text-muted mb-0">{project.description}</p>
        </div>
        <div className="ms-auto">
          <Button 
            variant="outline-secondary" 
            size="sm"
            onClick={() => navigate(`/projects/${id}/settings`)}
          >
            <Gear className="me-1" /> Settings
          </Button>
        </div>
      </div>

      {error && <Alert variant="danger" className="mb-4">{error}</Alert>}

      <Card className="mb-4">
        <Card.Body className="p-0">
          <Nav variant="tabs" defaultActiveKey="deployments" onSelect={(k) => setActiveTab(k)}>
            <Nav.Item>
              <Nav.Link eventKey="deployments" className={activeTab === 'deployments' ? 'active' : ''}>
                Deployments
              </Nav.Link>
            </Nav.Item>
            <Nav.Item>
              <Nav.Link eventKey="settings" className={activeTab === 'settings' ? 'active' : ''}>
                Settings
              </Nav.Link>
            </Nav.Item>
          </Nav>

          <div className="p-4">
            {activeTab === 'deployments' && (
              <div>
                <h5 className="mb-4">Deployment Platforms</h5>
                <p className="text-muted mb-4">
                  Select a platform to deploy your project. You can deploy to multiple platforms.
                </p>
                
                <Row>
                  {['github', 'netlify', 'vercel', 'cloudflare'].map((platform) => (
                    <Col md={6} lg={3} className="mb-4" key={platform}>
                      <DeploymentPlatform
                        platform={platform}
                        status={deploymentStatus[platform]}
                        onDeploy={handleDeploy}
                        loading={deploying}
                      />
                    </Col>
                  ))}
                </Row>
              </div>
            )}

            {activeTab === 'settings' && (
              <div>
                <h5 className="mb-4">Project Settings</h5>
                <p>Project settings will be available here.</p>
              </div>
            )}
          </div>
        </Card.Body>
      </Card>

      <Card className="border-danger">
        <Card.Body>
          <h5 className="text-danger mb-3">Danger Zone</h5>
          <div className="d-flex justify-content-between align-items-center">
            <div>
              <h6 className="mb-1">Delete this project</h6>
              <p className="text-muted small mb-0">
                Once you delete a project, there is no going back. Please be certain.
              </p>
            </div>
            <Button variant="outline-danger" size="sm">
              Delete Project
            </Button>
          </div>
        </Card.Body>
      </Card>
    </Container>
  );
};

export default ProjectDetail;