# Full Text Search Demo with Lucene.NET + React frontend

## Overview

This is a standalone Proof-of-Concept demonstration for full-text search using Lucene.NET in an ASP.NET Core backend and a React frontend. 

The system allows searching, adding, updating, deleting, and managing the index for an internal messaging system.

Built with simplicity in mind, this demo has no database, and utilizes Lucence.NET index directly.

## Features

* Full-Text Search (Lucene syntax supported)
* Add, Update, Delete Messages via UI/API
* Immediate Index Updates for CRUD operations
* Index Management (Optimize, Stats, Re-initialize) via UI/API

## Technology Stack

* **Backend:**
    * ASP.NET Core 8.0 (or latest recommended)
    * Lucene.NET 4.8.0-beta00017
* **Frontend:**
    * React (using Vite for setup) with JavaScript (ES6+) / JSX
    
## Project Structure
```
├─ backend/    # Root folder for the ASP.NET Core backend API
├    |─ ...
├    |─ lucene_index/  #local index file storage
├─ frontend/   # Root folder for the React UI (Vite)
└─ README.md
```
*(Note: The `lucene_index` directory is created automatically by the backend when it first runs or builds the index.)*

## Prerequisites

* .NET SDK 8.0+ ([Download](https://dotnet.microsoft.com/download))
* Node.js 18.x+ (with npm) or yarn ([Download](https://nodejs.org/))

## Getting Started

**(Run commands from the project's root directory)**

1.  **Start Backend API:**
    ```bash
    # Navigate to backend
    cd backend/

    # Run (handles restore/build automatically)
    dotnet run
    ```
    * Note the **Endpoint URL** (e.g., `http://localhost:5221`). Keep this terminal running.

2.  **Start Frontend UI:**
    ```bash
    # Navigate to frontend (from project root)
    cd frontend/

    # Install dependencies (choose one)
    npm install
    # or
    # yarn install

    # Configure backend URL (create .env.development.local file if needed)
    # Add this line to .env.development.local, replacing URL if necessary:
    # VITE_API_URL=http://localhost:5221

    # Run development server (choose one)
    npm run dev
    # or
    # yarn dev
    ```
    * Note the **Frontend URL** (e.g., `http://localhost:5173`). Keep this terminal running.

3.  **Access Application:**
    * Open your browser and go to the **Frontend URL** (e.g., `http://localhost:5173`).

## API Documentation (Swagger UI)

* While the backend is running, access interactive API documentation at:
    **`[Your Backend URL]/swagger`**
    (e.g., `http://localhost:5221/swagger`)
* Allows exploring and testing all API endpoints.

## Key Operations Guide

* **Search:** Use the main input field. Supports basic Lucene syntax (e.g., `david`, `recipient:bob` or  `sender:Alice`).
* **Add/Edit:** Use the form below the search results. Click "Edit" on a result to populate the form for updating.
* **Delete:** Click the "Delete" button next to a search result.
* **Admin:** Use the "Index Management" section for Stats, Optimization (ForceMerge), or Re-initializing the index with sample data (Warning: this will delete all current data).

## API Endpoint Reference

* `GET /api/search`: Search messages.
* `POST /api/messages`: Add a new message.
* `PUT /api/messages/{id}`: Update a message.
* `DELETE /api/messages/{id}`: Delete a message.
* `POST /api/admin/optimize`: Optimize the index segments.
* `GET /api/admin/stats`: Get index statistics.

## Lucene Index Notes

* Stored in the directory specified by `Lucene:IndexPath` in `backend/appsettings.Development.json` (defaults to `lucene_index`).
* Created automatically if it doesn't exist (using sample data).
* Updates (Add, Update, Delete) are committed and reflected in subsequent searches.
* Optimize merges segments (I/O intensive); Re-initialize resets the index.

## Configuration

* **Backend:** Index path (`Lucene:IndexPath`) in `appsettings.Development.json`.
* **Frontend:** Backend API URL (`VITE_API_URL`) in `.env.development.local`.

---