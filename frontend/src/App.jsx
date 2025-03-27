// src/App.jsx
import React, { useState, useCallback, useEffect, useRef } from 'react';
import axios from 'axios';
import './App.css';

// Define the API base URL - Ensure this points to your backend's HTTPS address
const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7123';

function App() {
  // --- State Variables ---
  // Search
  const [query, setQuery] = useState('');
  const [results, setResults] = useState(null);
  const [totalHits, setTotalHits] = useState(0);
  const [luceneQuery, setLuceneQuery] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const inputRef = useRef(null);

  // Add/Edit Message
  const [isEditing, setIsEditing] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [newMessage, setNewMessage] = useState({ sender: '', recipients: '', subject: '', body: '' });
  const [isAddingOrUpdating, setIsAddingOrUpdating] = useState(false);
  const [addEditError, setAddEditError] = useState('');
  const [addEditSuccess, setAddEditSuccess] = useState('');

  // Admin Actions
  const [isReindexing, setIsReindexing] = useState(false);
  const [reindexStatus, setReindexStatus] = useState('');
  const [isOptimizing, setIsOptimizing] = useState(false);
  const [optimizeStatus, setOptimizeStatus] = useState('');
  const [indexStats, setIndexStats] = useState(null);
  const [statsError, setStatsError] = useState('');
  const [isFetchingStats, setIsFetchingStats] = useState(false);

  // Focus on load
  useEffect(() => { if (inputRef.current) inputRef.current.focus(); }, []);

  // --- Utility Functions ---
  const clearAllFeedback = () => {
    setError(''); setAddEditError(''); setAddEditSuccess('');
    setReindexStatus(''); setOptimizeStatus(''); setStatsError('');
  };

  // Generic error handler for API calls
  const handleApiError = (err, actionDescription = 'perform action') => {
    console.error(`API Error during ${actionDescription}:`, err);
    let errorMessage = `Failed to ${actionDescription}.`;
    if (err.response) {
      if (err.response.data && typeof err.response.data === 'object') {
        const validationErrors = Object.values(err.response.data.errors || {}).flat();
        if (validationErrors.length > 0) { errorMessage = validationErrors.join(' '); }
        else if (err.response.data.title) { errorMessage = err.response.data.title; }
        else if (typeof err.response.data === 'string' && err.response.data.length < 200) { errorMessage = err.response.data; } // Use simple string response if available
        else { errorMessage += ` Status: ${err.response.status}`; }
      } else if (err.response.statusText) { errorMessage += ` Status: ${err.response.status} - ${err.response.statusText}`; }
      else { errorMessage += ` Status: ${err.response.status}`; }
    } else if (err.request) { errorMessage += ' No response from server.'; }
    else { errorMessage += ` ${err.message}`; }
    return errorMessage;
  };


  // --- Search Logic ---
  const handleSearch = useCallback(async (searchQuery = query) => { // Allow passing query override
    const trimmedQuery = searchQuery.trim();
    if (!trimmedQuery) return;
    setIsLoading(true); clearAllFeedback(); setResults(null);
    setTotalHits(0); setLuceneQuery('');
    try {
      const response = await axios.get(`${API_BASE_URL}/api/search`, { params: { q: trimmedQuery, limit: 20 } });
      if (response.data) {
        if (response.data.errorMessage) { setError(`Search Error: ${response.data.errorMessage}`); setResults(null); }
        else { setResults(response.data.hits || []); setTotalHits(response.data.totalHits || 0); setLuceneQuery(response.data.luceneQuery || ''); }
      } else { setError('Empty response.'); setResults(null); }
    } catch (err) { setError(handleApiError(err, 'fetch search results')); setResults(null); setTotalHits(0); }
    finally { setIsLoading(false); }
  }, [query]); // Keep query dependency for Enter key press

  const handleKeyPress = (event) => { if (event.key === 'Enter') handleSearch(); };


  // --- Add/Edit Message Logic ---
  const handleNewMessageChange = (e) => {
    const { name, value } = e.target;
    clearAllFeedback();
    setNewMessage(prev => ({ ...prev, [name]: value }));
  };

  const handleEditClick = (message) => {
    clearAllFeedback(); setIsEditing(true); setEditingId(message.id);
    setNewMessage({
      sender: message.sender, recipients: message.recipients.join(', '),
      subject: message.subject, body: message.body
    });
    // Scroll to the form for better UX
    const formElement = document.querySelector('.add-message-form');
    if (formElement) formElement.scrollIntoView({ behavior: 'smooth' });
  };

  const handleCancelEdit = () => {
    clearAllFeedback(); setIsEditing(false); setEditingId(null);
    setNewMessage({ sender: '', recipients: '', subject: '', body: '' });
  };

  const handleSaveMessage = async () => {
    clearAllFeedback();
    if (!newMessage.sender.trim() || !newMessage.recipients.trim() || !newMessage.subject.trim() || !newMessage.body.trim()) {
      setAddEditError("All fields are required."); return;
    }
    const recipientsArray = newMessage.recipients.split(',').map(r => r.trim()).filter(r => r !== '');
    if (recipientsArray.length === 0) { setAddEditError("Provide valid recipients."); return; }

    const payload = { sender: newMessage.sender.trim(), recipients: recipientsArray, subject: newMessage.subject.trim(), body: newMessage.body.trim() };
    setIsAddingOrUpdating(true);
    const url = isEditing ? `${API_BASE_URL}/api/messages/${editingId}` : `${API_BASE_URL}/api/messages`;
    const method = isEditing ? 'put' : 'post';
    const actionDesc = isEditing ? 'update message' : 'add message';

    try {
      const response = await axios[method](url, payload);
      if (response.status === 200 || response.status === 201) {
        setAddEditSuccess(`Message ${isEditing ? 'updated' : 'added'} successfully! ID: ${response.data.id}`);
        handleCancelEdit(); // Clear form and reset editing state
        handleSearch(query); // Refresh current search results automatically
      } else { setAddEditError(`Unexpected status: ${response.status}`); }
    } catch (err) { setAddEditError(handleApiError(err, actionDesc)); }
    finally { setIsAddingOrUpdating(false); }
  };

  // --- Delete Message Logic ---
  const handleDeleteMessage = async (messageId, messageSubject) => {
    if (!window.confirm(`DELETE message "${messageSubject || 'No Subject'}" (ID: ${messageId})?\n\nThis cannot be undone.`)) return;
    clearAllFeedback();
    // Indicate loading (could refine to show loading on the specific item)
    setIsLoading(true); // Reuse general loading indicator
    try {
      const response = await axios.delete(`${API_BASE_URL}/api/messages/${messageId}`);
      if (response.status === 204) { // No Content success
        setAddEditSuccess(`Message ID ${messageId} deleted.`); // Show feedback
        // Immediate UI update: Remove from results list
        setResults(prevResults => prevResults ? prevResults.filter(hit => hit.message.id !== messageId) : null);
        setTotalHits(prev => Math.max(0, prev - 1));
      } else { setAddEditError(`Delete returned status: ${response.status}`); }
    } catch (err) { setAddEditError(handleApiError(err, 'delete message')); }
    finally { setIsLoading(false); } // Turn off loading indicator
  };

  // --- Admin Actions Logic ---
  const handleReindex = async () => {
    if (!window.confirm("RE-INITIALIZE INDEX?\n\nThis DELETES all current indexed data and replaces it with initial samples.")) return;
    setIsReindexing(true); clearAllFeedback(); setResults(null); setIndexStats(null); // Clear results and stats
    try {
      const response = await axios.post(`${API_BASE_URL}/api/search/reindex`);
      if (response.status === 200) setReindexStatus(response.data || "Index re-initialized.");
      else setReindexStatus(`Unexpected status: ${response.status}`);
    } catch (err) { setReindexStatus(`Error: ${handleApiError(err, 're-initialize index')}`); }
    finally { setIsReindexing(false); }
  };

  const handleOptimizeIndex = async () => {
    if (!window.confirm("OPTIMIZE INDEX (Force Merge)?\n\nThis can take time and blocks other index changes.")) return;
    setIsOptimizing(true); clearAllFeedback();
    try {
      const response = await axios.post(`${API_BASE_URL}/api/admin/optimize`);
      if (response.status === 200) setOptimizeStatus(response.data || "Index optimization initiated.");
      else setOptimizeStatus(`Unexpected status: ${response.status}`);
    } catch (err) { setOptimizeStatus(`Error: ${handleApiError(err, 'optimize index')}`); }
    finally { setIsOptimizing(false); }
  };

  const handleFetchStats = async () => {
    setIsFetchingStats(true); clearAllFeedback(); setIndexStats(null);
    try {
      const response = await axios.get(`${API_BASE_URL}/api/admin/stats`);
      if (response.status === 200 && response.data) { setIndexStats(response.data); }
      else { setStatsError(`Failed to fetch stats. Status: ${response.status}`); }
    } catch (err) { setStatsError(handleApiError(err, 'fetch index stats')); }
    finally { setIsFetchingStats(false); }
  };

  // --- JSX Rendering ---
  return (
    <div className="App">
      <h1>Search demo w Lucene.NET</h1>

      {/* --- Search Section --- */}
      <div className="search-bar">
        <input ref={inputRef} type="text" value={query} onChange={(e) => setQuery(e.target.value)} onKeyDown={handleKeyPress} placeholder="enter search term e.g. mars, sender:alice or recipient:bob ..." disabled={isLoading} />
        <button onClick={() => handleSearch()} disabled={isLoading || !query.trim()}> {isLoading ? 'Searching...' : 'Search'} </button>
      </div>
      {error && <p className="error-message">{error}</p>}
      {isLoading && <p>Searching...</p>}
      {!isLoading && !error && results !== null && (
        <div className="results-info">
          {luceneQuery && <p><small>Parsed Query: <code>{luceneQuery}</code></small></p>}
          {totalHits > 0 && <p>Showing {results.length} of approx {totalHits} results.</p>}
          {results.length === 0 && query.trim() && <p>No results found for "{query.trim()}".</p>}
        </div>
      )}
      <div className="results-list">
        {results && results.length > 0 && results.map((hit) => (
          <div key={hit.message.id} className="message-hit">
            <div className="message-actions">
              <button onClick={() => handleEditClick(hit.message)} className="action-button edit-button" title="Edit Message">Edit</button>
              <button onClick={() => handleDeleteMessage(hit.message.id, hit.message.subject)} className="action-button delete-button" title="Delete Message">Delete</button>
            </div>
            <p className="score">Score: {hit.score.toFixed(4)}</p>
            <p><strong>ID:</strong> {hit.message.id}</p>
            <p><strong>From:</strong> {hit.message.sender}</p>
            <p><strong>To:</strong> {hit.message.recipients.join(', ')}</p>
            <p><strong>Subject:</strong> {hit.message.subject}</p>
            <p><strong>Date:</strong> {new Date(hit.message.timestamp).toLocaleString()}</p>
            <p className="body">{hit.message.body}</p> {/* Assumes Body is stored */}
          </div>
        ))}
      </div>

      <hr />

      {/* --- Add/Edit Message Section --- */}
      <div className="add-message-form">
        <h2>{isEditing ? 'Edit Message' : 'Add New Message'}</h2>
        {addEditSuccess && <p className="success-message">{addEditSuccess}</p>}
        {addEditError && <p className="error-message">{addEditError}</p>}
        {isEditing && <p><em>Editing message ID: {editingId}</em></p>}
        <div><label htmlFor="senderInput">Sender:</label><input id="senderInput" type="text" name="sender" value={newMessage.sender} onChange={handleNewMessageChange} disabled={isAddingOrUpdating} /></div>
        <div><label htmlFor="recipientsInput">Recipients (comma-separated):</label><input id="recipientsInput" type="text" name="recipients" value={newMessage.recipients} onChange={handleNewMessageChange} disabled={isAddingOrUpdating} placeholder="e.g., bob, charlie" /></div>
        <div><label htmlFor="subjectInput">Subject:</label><input id="subjectInput" type="text" name="subject" value={newMessage.subject} onChange={handleNewMessageChange} disabled={isAddingOrUpdating} /></div>
        <div><label htmlFor="bodyInput">Body:</label><textarea id="bodyInput" name="body" value={newMessage.body} onChange={handleNewMessageChange} disabled={isAddingOrUpdating} rows={4} /></div>
        <div className="form-actions">
          <button onClick={handleSaveMessage} disabled={isAddingOrUpdating || !newMessage.sender.trim() || !newMessage.recipients.trim() || !newMessage.subject.trim() || !newMessage.body.trim()}>
            {isAddingOrUpdating ? (isEditing ? 'Updating...' : 'Adding...') : (isEditing ? 'Update Message' : 'Add Message to Index')}
          </button>
          {isEditing && (
            <button onClick={handleCancelEdit} disabled={isAddingOrUpdating} className="cancel-button">Cancel Edit</button>
          )}
        </div>
      </div>

      <hr />

      {/* --- Admin Actions Section --- */}
      <div className="admin-section">
        <h2>Index Management / Admin</h2>

        {/* Stats */}
        <div className="admin-action">
          <h3>Index Statistics</h3>
          {statsError && <p className="error-message">{statsError}</p>}
          {isFetchingStats && <p>Fetching stats...</p>}
          {indexStats && !isFetchingStats && (
            <div className="stats-display">
              <p>Live Documents: <strong>{indexStats.numDocs}</strong></p>
              <p>Total Docs (incl. deleted): <strong>{indexStats.maxDocs}</strong></p>
              <p>Segments: <strong>{indexStats.numSegments}</strong> {indexStats.isOptimized ? '(Optimized)' : ''}</p>
            </div>
          )}
          <button onClick={handleFetchStats} disabled={isFetchingStats} className="getstats-button">
            {isFetchingStats ? 'Fetching...' : 'Refresh Index Stats'}
          </button>
        </div>

        {/* Optimize */}
        <div className="admin-action">
          <h3>Optimize Index</h3>
          {optimizeStatus && <p className={optimizeStatus.startsWith('Error:') ? 'error-message' : 'success-message'}>{optimizeStatus}</p>}
          <button onClick={handleOptimizeIndex} disabled={isOptimizing} className="optimize-button">
            {isOptimizing ? 'Optimizing...' : 'Optimize Index (Force Merge)'}
          </button>
          <p><small>Merges index segments. Can take time and block other index operations.</small></p>
        </div>

        {/* Re-index */}
        <div className="admin-action">
          <h3>Re-Initialize Index</h3>
          {reindexStatus && <p className={reindexStatus.startsWith('Error:') ? 'error-message' : 'success-message'}>{reindexStatus}</p>}
          <button onClick={handleReindex} disabled={isReindexing} className="reindex-button">
            {isReindexing ? 'Re-indexing...' : 'Re-initialize with Sample Data'}
          </button>
          <p><small>Warning: Deletes all current index data and replaces it with samples.</small></p>
        </div>
      </div>

    </div> // End App div
  );
}

export default App;