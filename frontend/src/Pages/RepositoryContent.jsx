import React, { useState, useEffect } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';

const RepositoryContent = () => {
    const { owner, repoName, '*': path } = useParams();
    const navigate = useNavigate();
    const location = useLocation();
    const [contents, setContents] = useState([]);
    const [fileContent, setFileContent] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [currentPath, setCurrentPath] = useState('');
    const [breadcrumbs, setBreadcrumbs] = useState([]);

    // Get token from localStorage or cookies
    const getAuthToken = () => {
        // Try localStorage first
        let token = localStorage.getItem('githubToken');
        
        // If not in localStorage, try to get from cookies
        if (!token) {
            const cookies = document.cookie.split(';');
            const tokenCookie = cookies.find(cookie => cookie.trim().startsWith('GitHubAccessToken='));
            if (tokenCookie) {
                token = tokenCookie.split('=')[1];
            }
        }
        
        return token;
    };

    // Build breadcrumbs from current path
    const buildBreadcrumbs = (path) => {
        if (!path) return [{ name: owner + '/' + repoName, path: '' }];
        
        const parts = path.split('/');
        const crumbs = [{ name: owner + '/' + repoName, path: '' }];
        
        let currentPath = '';
        parts.forEach((part, index) => {
            currentPath += (index === 0 ? '' : '/') + part;
            crumbs.push({ name: part, path: currentPath });
        });
        
        return crumbs;
    };

    // Fetch repository contents
    const fetchContents = async (targetPath = '') => {
        setLoading(true);
        setError(null);
        setFileContent(null);
        
        const token = getAuthToken();
        if (!token) {
            setError('No authentication token found. Please login first.');
            setLoading(false);
            return;
        }

        try {
            const apiPath = targetPath 
                ? `api/repositories/contents/${owner}/${repoName}/${targetPath}`
                : `api/repositories/contents/${owner}/${repoName}`;
            
            const response = await fetch(`http://localhost:5280/${apiPath}`, {
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const data = await response.json();
            
            // Check if it's a single file
            if (data.length === 1 && data[0].type === 'file') {
                await fetchFileContent(targetPath);
            } else {
                setContents(data);
                setCurrentPath(targetPath);
                setBreadcrumbs(buildBreadcrumbs(targetPath));
            }
        } catch (err) {
            setError(`Failed to fetch repository contents: ${err.message}`);
        } finally {
            setLoading(false);
        }
    };

    // Fetch file content
    const fetchFileContent = async (filePath) => {
        const token = getAuthToken();
        if (!token) {
            setError('No authentication token found. Please login first.');
            return;
        }

        try {
            const response = await fetch(`http://localhost:5280/api/repositories/file/${owner}/${repoName}/${filePath}`, {
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const content = await response.text();
            setFileContent(content);
            setCurrentPath(filePath);
            setBreadcrumbs(buildBreadcrumbs(filePath));
        } catch (err) {
            setError(`Failed to fetch file content: ${err.message}`);
        }
    };

    // Handle item click (folder or file)
    const handleItemClick = (item) => {
        // Extract actual values from potential objects
        const itemType = typeof item.type === 'object' ? item.type.value || item.type.stringValue || 'unknown' : item.type;
        const itemName = typeof item.name === 'object' ? item.name.value || item.name.stringValue || 'Unknown' : item.name;
        
        if (itemType === 'dir') {
            const newPath = currentPath ? `${currentPath}/${itemName}` : itemName;
            navigate(`/repository/${owner}/${repoName}/${newPath}`);
        } else if (itemType === 'file') {
            const filePath = currentPath ? `${currentPath}/${itemName}` : itemName;
            fetchFileContent(filePath);
        }
    };

    // Handle breadcrumb click
    const handleBreadcrumbClick = (breadcrumb) => {
        if (breadcrumb.path === '') {
            navigate(`/repository/${owner}/${repoName}`);
        } else {
            navigate(`/repository/${owner}/${repoName}/${breadcrumb.path}`);
        }
    };

    // Format file size - handle both number and object formats
    const formatFileSize = (sizeValue) => {
        // Handle case where size might be an object with stringValue/value properties
        let bytes = sizeValue;
        if (typeof sizeValue === 'object' && sizeValue !== null) {
            bytes = sizeValue.value || sizeValue.stringValue || 0;
        }
        
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    };

    // Get file extension for syntax highlighting hints
    const getFileExtension = (filename) => {
        return filename.split('.').pop().toLowerCase();
    };

    useEffect(() => {
        const pathFromUrl = path || '';
        fetchContents(pathFromUrl);
    }, [owner, repoName, path]);

    if (loading) {
        return <div>Loading repository content...</div>;
    }

    if (error) {
        return (
            <div>
                <h2>Error</h2>
                <p>{error}</p>
                <button onClick={() => navigate('/repositories')}>Back to Repositories</button>
            </div>
        );
    }

    return (
        <div>
            <h1>Repository Content</h1>
            
            {/* Breadcrumb Navigation */}
            <nav>
                {breadcrumbs.map((crumb, index) => (
                    <span key={index}>
                        {index > 0 && ' / '}
                        <button 
                            onClick={() => handleBreadcrumbClick(crumb)}
                            style={{ background: 'none', border: 'none', color: 'blue', textDecoration: 'underline', cursor: 'pointer' }}
                        >
                            {crumb.name}
                        </button>
                    </span>
                ))}
            </nav>

            {/* File Content Display */}
            {fileContent !== null && (
                <div style={{ marginTop: '20px' }}>
                    <div style={{ 
                        display: 'flex', 
                        justifyContent: 'space-between', 
                        alignItems: 'center',
                        marginBottom: '10px',
                        padding: '10px',
                        background: '#f8f9fa',
                        border: '1px solid #dee2e6',
                        borderRadius: '4px 4px 0 0'
                    }}>
                        <h3 style={{ margin: 0 }}>📄 {currentPath}</h3>
                        <span style={{ 
                            fontSize: '12px', 
                            color: '#6c757d',
                            textTransform: 'uppercase'
                        }}>
                            {getFileExtension(currentPath)}
                        </span>
                    </div>
                    <pre style={{ 
                        background: '#ffffff', 
                        padding: '20px', 
                        border: '1px solid #dee2e6', 
                        borderTop: 'none',
                        borderRadius: '0 0 4px 4px',
                        overflow: 'auto',
                        maxHeight: '70vh',
                        fontSize: '14px',
                        lineHeight: '1.5',
                        margin: 0
                    }}>
                        {fileContent}
                    </pre>
                    <div style={{ marginTop: '15px', display: 'flex', gap: '10px' }}>
                        <button 
                            onClick={() => {
                                const parentPath = currentPath.split('/').slice(0, -1).join('/');
                                if (parentPath) {
                                    navigate(`/repository/${owner}/${repoName}/${parentPath}`);
                                } else {
                                    navigate(`/repository/${owner}/${repoName}`);
                                }
                            }}
                            style={{
                                padding: '8px 16px',
                                backgroundColor: '#007bff',
                                color: 'white',
                                border: 'none',
                                borderRadius: '4px',
                                cursor: 'pointer'
                            }}
                        >
                            ← Back to Directory
                        </button>
                        <button 
                            onClick={() => setFileContent(null)}
                            style={{
                                padding: '8px 16px',
                                backgroundColor: '#6c757d',
                                color: 'white',
                                border: 'none',
                                borderRadius: '4px',
                                cursor: 'pointer'
                            }}
                        >
                            Close File
                        </button>
                    </div>
                </div>
            )}

            {/* Directory Contents Display */}
            {fileContent === null && contents.length > 0 && (
                <div>
                    <h3>Contents</h3>
                    <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                        <thead>
                            <tr style={{ borderBottom: '1px solid #ddd' }}>
                                <th style={{ textAlign: 'left', padding: '8px' }}>Name</th>
                                <th style={{ textAlign: 'left', padding: '8px' }}>Type</th>
                                <th style={{ textAlign: 'left', padding: '8px' }}>Size</th>
                            </tr>
                        </thead>
                        <tbody>
                            {/* Show parent directory link if not at root */}
                            {currentPath && (
                                <tr style={{ borderBottom: '1px solid #eee' }}>
                                    <td style={{ padding: '8px' }}>
                                        <button 
                                            onClick={() => {
                                                const parentPath = currentPath.split('/').slice(0, -1).join('/');
                                                if (parentPath) {
                                                    navigate(`/repository/${owner}/${repoName}/${parentPath}`);
                                                } else {
                                                    navigate(`/repository/${owner}/${repoName}`);
                                                }
                                            }}
                                            style={{ background: 'none', border: 'none', color: 'blue', textDecoration: 'underline', cursor: 'pointer' }}
                                        >
                                            📁 ..
                                        </button>
                                    </td>
                                    <td style={{ padding: '8px' }}>directory</td>
                                    <td style={{ padding: '8px' }}>-</td>
                                </tr>
                            )}
                            
                            {/* Sort directories first, then files */}
                            {contents
                                .sort((a, b) => {
                                    // Extract actual values from potential objects
                                    const aType = typeof a.type === 'object' ? a.type.value || a.type.stringValue || 'unknown' : a.type;
                                    const bType = typeof b.type === 'object' ? b.type.value || b.type.stringValue || 'unknown' : b.type;
                                    const aName = typeof a.name === 'object' ? a.name.value || a.name.stringValue || 'Unknown' : a.name;
                                    const bName = typeof b.name === 'object' ? b.name.value || b.name.stringValue || 'Unknown' : b.name;
                                    
                                    if (aType === bType) {
                                        return aName.localeCompare(bName);
                                    }
                                    return aType === 'dir' ? -1 : 1;
                                })
                                .map((item) => (
                                    <tr key={typeof item.name === 'object' ? item.name.value || item.name.stringValue || Math.random() : item.name} style={{ borderBottom: '1px solid #eee' }}>
                                        <td style={{ padding: '8px' }}>
                                            <button 
                                                onClick={() => handleItemClick(item)}
                                                style={{ background: 'none', border: 'none', color: 'blue', textDecoration: 'underline', cursor: 'pointer' }}
                                            >
                                                {(typeof item.type === 'object' ? item.type.value || item.type.stringValue || 'unknown' : item.type) === 'dir' ? '📁' : '📄'} {typeof item.name === 'object' ? item.name.value || item.name.stringValue || 'Unknown' : item.name}
                                            </button>
                                        </td>
                                        <td style={{ padding: '8px' }}>{typeof item.type === 'object' ? item.type.value || item.type.stringValue || 'unknown' : item.type}</td>
                                        <td style={{ padding: '8px' }}>
                                            {(typeof item.type === 'object' ? item.type.value || item.type.stringValue || 'unknown' : item.type) === 'file' ? formatFileSize(item.size) : '-'}
                                        </td>
                                    </tr>
                                ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Empty directory */}
            {fileContent === null && contents.length === 0 && (
                <div>
                    <p>This directory is empty.</p>
                </div>
            )}

            <div style={{ marginTop: '20px' }}>
                <button onClick={() => navigate('/repositories')}>
                    Back to Repositories
                </button>
            </div>
        </div>
    );
};

export default RepositoryContent;
