// src/components/RepoContents.jsx
import React, { useState, useEffect } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
// Import React Icons
import { FaFolder, FaFileAlt, FaChevronLeft, FaCode, FaFilePdf, FaImage, FaFileArchive, FaFile } from 'react-icons/fa';

const RepoContents = () => {
    const { owner, repoName } = useParams(); // Get owner and repoName from URL params
    const navigate = useNavigate();
    const location = useLocation(); // To get current path from URL search params or state

    // State to manage the current path within the repository
    // We'll use URL search params to manage the path for easier sharing/refreshing
    const [currentPath, setCurrentPath] = useState('');
    const [contents, setContents] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [fileContent, setFileContent] = useState(null); // New state for file content
    const [viewingFile, setViewingFile] = useState(false); // New state to track if we're viewing a file
    const [currentFileName, setCurrentFileName] = useState(''); // New state for current file name

    // Effect to parse the 'path' from the URL query parameter
    useEffect(() => {
        const params = new URLSearchParams(location.search);
        const pathFromUrl = params.get('path') || '';
        setCurrentPath(pathFromUrl);
        // Reset file view when path changes (navigating directories)
        setFileContent(null);
        setViewingFile(false);
        setCurrentFileName('');
    }, [location.search]);

    // Effect to fetch repository contents whenever owner, repoName, or currentPath changes
    useEffect(() => {
        const fetchContents = async () => {
            setLoading(true);
            setError(null);
            setContents([]); // Clear previous contents
            setFileContent(null); // Clear any file content
            setViewingFile(false); // Not viewing a file
            setCurrentFileName(''); // Clear file name

            const token = localStorage.getItem('token');

            if (!token) {
                navigate('/'); // Redirect to login if no token
                return;
            }

            try {
                // *** FIX APPLIED HERE: Changed to use the /contents/ endpoint ***
                const apiUrl = `https://localhost:7198/api/repositories/${owner}/${repoName}/contents/${currentPath}`;
                
                const response = await fetch(apiUrl, {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    const errorText = await response.text();
                    if (response.status === 401) {
                        throw new Error('Unauthorized: Session expired. Please log in again.');
                    }
                    throw new Error(`Failed to fetch contents: ${response.status} ${errorText}`);
                }

                const data = await response.json();
                setContents(data);
            } catch (err) {
                console.error('Failed to fetch repository contents:', err);
                setError(err.message);
                if (err.message.includes('Unauthorized')) {
                    localStorage.removeItem('token'); // Clear token if unauthorized
                    navigate('/');
                }
            } finally {
                setLoading(false);
            }
        };

        if (owner && repoName) {
            fetchContents();
        }
    }, []);

    // Function to fetch and display file content
    const fetchFileContent = async (fileName, filePath) => {
        setLoading(true);
        setError(null);
        setFileContent(null); // Clear previous file content
        setViewingFile(true); // Indicate we are viewing a file
        setCurrentFileName(fileName); // Set the name of the file being viewed

        const token = localStorage.getItem('token');
        if (!token) {
            navigate('/');
            return;
        }

        try {
            // This URL is correct for fetching raw file content
            const apiUrl = `https://localhost:7198/api/repositories/${owner}/${repoName}/file/${filePath}`;
            const response = await fetch(apiUrl, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                const errorText = await response.text();
                if (response.status === 401) {
                    throw new Error('Unauthorized: Session expired. Please log in again.');
                }
                throw new Error(`Failed to fetch file content: ${response.status} ${errorText}`);
            }

            const content = await response.text(); // Get raw text content
            console.log('File content fetched:', content);
            setFileContent(content);
        } catch (err) {
            console.error('Failed to fetch file content:', err);
            setError(err.message);
            if (err.message.includes('Unauthorized')) {
                localStorage.removeItem('token');
                navigate('/');
            }
        } finally {
            setLoading(false);
        }
    };

    // Function to navigate into a directory or display file content
    const navigateToPath = (type, name, path) => {
        if (type === 'dir') {
            const newPath = currentPath ? `${currentPath}/${name}` : name;
            // Update URL query parameter to reflect the new path
            navigate(`/repositories/${owner}/${repoName}?path=${newPath}`);
        } else {
            // For files, fetch and display their content
            fetchFileContent(name, path);
        }
    };

    // Function to go back up one directory level or back to contents from file view
    const goBack = () => {
        if (viewingFile) {
            setFileContent(null);
            setViewingFile(false);
            setCurrentFileName('');
            // No navigation needed, just switch view
        } else if (currentPath) {
            const pathSegments = currentPath.split('/');
            pathSegments.pop(); // Remove the last segment
            const newPath = pathSegments.join('/');
            if (newPath === '') {
                navigate(`/repositories/${owner}/${repoName}`); // Go back to root if no more segments
            } else {
                navigate(`/repositories/${owner}/${repoName}?path=${newPath}`);
            }
        }
    };

    // Helper to get appropriate icon based on file type/extension
    const getFileIcon = (fileName) => {
        const extension = fileName.split('.').pop().toLowerCase();
        switch (extension) {
            case 'js':
            case 'jsx':
            case 'ts':
            case 'tsx':
            case 'cs':
            case 'py':
            case 'java':
            case 'html':
            case 'css':
            case 'json':
            case 'xml':
                return <FaCode className="h-6 w-6 text-yellow-600 mr-3" />;
            case 'pdf':
                return <FaFilePdf className="h-6 w-6 text-red-500 mr-3" />;
            case 'png':
            case 'jpg':
            case 'jpeg':
            case 'gif':
            case 'svg':
                return <FaImage className="h-6 w-6 text-purple-500 mr-3" />;
            case 'zip':
            case 'rar':
            case '7z':
                return <FaFileArchive className="h-6 w-6 text-gray-500 mr-3" />;
            case 'md':
            case 'txt':
                return <FaFileAlt className="h-6 w-6 text-blue-500 mr-3" />;
            default:
                return <FaFile className="h-6 w-6 text-gray-400 mr-3" />;
        }
    };

    if (loading) {
        return <div className="p-4 text-center text-gray-600">Loading...</div>;
    }

    if (error) {
        return <div className="p-4 text-center text-red-500">Error: {error}</div>;
    }

    return (
        <div className="container mx-auto p-4 bg-gray-50 min-h-screen rounded-lg shadow-md">
            <h1 className="text-3xl font-bold text-gray-800 mb-4">
                <span className="text-blue-600">{owner}</span> / <span className="text-green-600">{repoName}</span>
            </h1>
            <h2 className="text-xl font-semibold text-gray-700 mb-4">
                {viewingFile ? `Viewing File: ${currentFileName}` : `Contents of: /${currentPath || ''}`}
            </h2>

            {(currentPath || viewingFile) && ( // Show back button if in a sub-directory or viewing a file
                <button
                    onClick={goBack}
                    className="flex items-center px-4 py-2 mb-4 bg-gray-200 text-gray-700 rounded-md hover:bg-gray-300 transition-colors duration-200"
                >
                    <FaChevronLeft className="h-4 w-4 mr-2" /> Back
                </button>
            )}

            {viewingFile && fileContent !== null ? (
                <div className="bg-white p-4 rounded-lg shadow-sm overflow-auto max-h-[70vh]">
                    <pre className="text-sm font-mono whitespace-pre-wrap break-words">
                        {fileContent}
                    </pre>
                </div>
            ) : contents.length > 0 ? (
                <ul className="space-y-2">
                    {contents.map((item) => (
                        <li
                            key={item.path} // Use item.path as a unique key
                            className="flex items-center p-3 bg-white rounded-lg shadow-sm hover:shadow-md transition-shadow duration-200 cursor-pointer"
                            onClick={() => navigateToPath(item.type, item.name, item.path)}
                        >
                            {item.type === 'dir' ? (
                                <FaFolder className="h-6 w-6 text-blue-500 mr-3" />
                            ) : (
                                getFileIcon(item.name) // Use helper for file icons
                            )}
                            <span className="font-medium text-gray-800">
                                {item.name}
                            </span>
                            {item.type === 'file' && (
                                <span className="ml-auto text-sm text-gray-500">
                                    ({item.size} bytes)
                                </span>
                            )}
                        </li>
                    ))}
                </ul>
            ) : (
                <p className="text-gray-600">This directory is empty.</p>
            )}
        </div>
    );
};

export default RepoContents;
