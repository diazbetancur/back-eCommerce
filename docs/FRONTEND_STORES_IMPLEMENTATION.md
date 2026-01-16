# 🏪 Guía de Implementación Frontend - Sistema de Stores

## 📋 Tabla de Contenidos
1. [Visión General](#visión-general)
2. [Autenticación y Configuración](#autenticación-y-configuración)
3. [Rutas a Implementar](#rutas-a-implementar)
4. [Componentes Sugeridos](#componentes-sugeridos)
5. [API Endpoints y Ejemplos](#api-endpoints-y-ejemplos)
6. [Flujos de Usuario](#flujos-de-usuario)
7. [Manejo de Errores](#manejo-de-errores)
8. [Testing Recomendado](#testing-recomendado)

---

## 🎯 Visión General

### ¿Qué vas a implementar?

Un sistema de gestión de **inventario multi-ubicación** que permite:
- **Admin Panel**: Gestionar tiendas y stock por ubicación
- **Checkout**: Opcionalmente seleccionar tienda para recoger productos

### ⚠️ MUY IMPORTANTE - No romper nada existente

El backend está diseñado con **100% backward compatibility**:
- ✅ Si NO usas `storeId` en checkout → funciona como siempre (stock global)
- ✅ Si SÍ usas `storeId` → usa stock de esa tienda específica
- ✅ Productos sin stock en tiendas → siguen usando `Product.Stock`

**Regla de oro**: Todas las funcionalidades nuevas son **OPCIONALES**. No debes modificar flujos existentes.

---

## 🔐 Autenticación y Configuración

### Headers Requeridos en Todas las Peticiones

```javascript
const apiConfig = {
  baseURL: process.env.REACT_APP_API_URL || 'http://localhost:5093',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${getToken()}`, // JWT token del usuario logueado
    'X-Tenant-Slug': getTenantSlug() // Slug del tenant actual
  }
};
```

### Setup de Axios (Recomendado)

```javascript
// src/services/api.js
import axios from 'axios';

const api = axios.create({
  baseURL: process.env.REACT_APP_API_URL || 'http://localhost:5093',
  headers: {
    'Content-Type': 'application/json'
  }
});

// Interceptor para agregar token automáticamente
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('authToken');
    const tenantSlug = localStorage.getItem('tenantSlug');
    
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    
    if (tenantSlug) {
      config.headers['X-Tenant-Slug'] = tenantSlug;
    }
    
    return config;
  },
  (error) => Promise.reject(error)
);

// Interceptor para manejar errores globales
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Token expirado o inválido
      localStorage.removeItem('authToken');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default api;
```

### Verificación de Permisos

El backend valida permisos del módulo `inventory`:
- `inventory:view` → Ver tiendas y stock
- `inventory:create` → Crear tiendas
- `inventory:update` → Editar tiendas y stock
- `inventory:delete` → Eliminar tiendas

**Recomendación**: Ocultar botones/secciones según permisos del usuario.

```javascript
// src/hooks/usePermissions.js
export const usePermissions = () => {
  const user = useAuthStore(state => state.user);
  
  const hasPermission = (module, action) => {
    // Implementar lógica según tu estructura de usuario
    return user?.permissions?.[module]?.[action] === true;
  };
  
  return { hasPermission };
};

// Uso en componente
const { hasPermission } = usePermissions();

{hasPermission('inventory', 'create') && (
  <button onClick={handleCreateStore}>Nueva Tienda</button>
)}
```

---

## 🗺️ Rutas a Implementar

### 1. Admin Panel - Gestión de Tiendas

```javascript
// Rutas sugeridas (ajusta según tu estructura)
<Route path="/admin/stores" element={<StoresListPage />} />
<Route path="/admin/stores/new" element={<CreateStorePage />} />
<Route path="/admin/stores/:id" element={<StoreDetailPage />} />
<Route path="/admin/stores/:id/edit" element={<EditStorePage />} />
<Route path="/admin/stores/:id/stock" element={<StoreStockPage />} />
```

### 2. Admin Panel - Stock por Producto

```javascript
// Opción 1: Vista desde productos
<Route path="/admin/products/:id/stock" element={<ProductStockByStoresPage />} />

// Opción 2: Agregar tab en detalle de producto
// En tu componente ProductDetailPage, agregar:
<Tabs>
  <Tab label="General">...</Tab>
  <Tab label="Stock por Tienda">
    <ProductStockByStores productId={id} />
  </Tab>
</Tabs>
```

### 3. Checkout (Opcional)

```javascript
// NO crear nueva ruta, solo agregar componente en checkout existente
// En tu CheckoutPage.jsx, agregar:
<StoreSelector 
  cartItems={cartItems}
  onStoreSelect={setSelectedStore}
/>
```

---

## 🧩 Componentes Sugeridos

### 1. StoresList Component

Vista principal de tiendas.

```jsx
// src/components/admin/stores/StoresList.jsx
import React, { useState, useEffect } from 'react';
import api from '../../../services/api';
import { toast } from 'react-toastify';

const StoresList = () => {
  const [stores, setStores] = useState([]);
  const [loading, setLoading] = useState(true);
  const [includeInactive, setIncludeInactive] = useState(false);

  useEffect(() => {
    fetchStores();
  }, [includeInactive]);

  const fetchStores = async () => {
    setLoading(true);
    try {
      const { data } = await api.get('/admin/stores', {
        params: { includeInactive }
      });
      setStores(data);
    } catch (error) {
      toast.error('Error al cargar tiendas');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleSetDefault = async (storeId) => {
    try {
      await api.post(`/admin/stores/${storeId}/set-default`);
      toast.success('Tienda predeterminada actualizada');
      fetchStores(); // Recargar lista
    } catch (error) {
      toast.error(error.response?.data?.detail || 'Error al actualizar');
    }
  };

  const handleDelete = async (storeId) => {
    if (!window.confirm('¿Estás seguro de eliminar esta tienda?')) return;
    
    try {
      await api.delete(`/admin/stores/${storeId}`);
      toast.success('Tienda eliminada');
      fetchStores();
    } catch (error) {
      const message = error.response?.data?.detail || 'Error al eliminar';
      toast.error(message);
    }
  };

  if (loading) return <div>Cargando tiendas...</div>;

  return (
    <div className="stores-list">
      <div className="header">
        <h1>Gestión de Tiendas</h1>
        <button onClick={() => navigate('/admin/stores/new')}>
          + Nueva Tienda
        </button>
      </div>

      <div className="filters">
        <label>
          <input
            type="checkbox"
            checked={includeInactive}
            onChange={(e) => setIncludeInactive(e.target.checked)}
          />
          Incluir tiendas inactivas
        </label>
      </div>

      {stores.length === 0 ? (
        <div className="empty-state">
          <p>No hay tiendas registradas</p>
          <button onClick={() => navigate('/admin/stores/new')}>
            Crear primera tienda
          </button>
        </div>
      ) : (
        <div className="stores-grid">
          {stores.map(store => (
            <div key={store.id} className={`store-card ${!store.isActive ? 'inactive' : ''}`}>
              <div className="store-header">
                <h3>{store.name}</h3>
                {store.isDefault && (
                  <span className="badge badge-primary">Predeterminada</span>
                )}
                {!store.isActive && (
                  <span className="badge badge-danger">Inactiva</span>
                )}
              </div>
              
              <div className="store-info">
                {store.code && <p><strong>Código:</strong> {store.code}</p>}
                {store.address && (
                  <p>
                    <strong>Dirección:</strong> {store.address}
                    {store.city && `, ${store.city}`}
                  </p>
                )}
                {store.phone && <p><strong>Teléfono:</strong> {store.phone}</p>}
              </div>

              <div className="store-actions">
                <button onClick={() => navigate(`/admin/stores/${store.id}/stock`)}>
                  Ver Stock
                </button>
                <button onClick={() => navigate(`/admin/stores/${store.id}/edit`)}>
                  Editar
                </button>
                {!store.isDefault && (
                  <button onClick={() => handleSetDefault(store.id)}>
                    Predeterminar
                  </button>
                )}
                {!store.isDefault && (
                  <button 
                    onClick={() => handleDelete(store.id)}
                    className="btn-danger"
                  >
                    Eliminar
                  </button>
                )}
              </div>

              <div className="store-meta">
                <small>Creada: {new Date(store.createdAt).toLocaleDateString()}</small>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default StoresList;
```

### 2. CreateStore / EditStore Component

Formulario para crear/editar tiendas.

```jsx
// src/components/admin/stores/StoreForm.jsx
import React, { useState, useEffect } from 'react';
import api from '../../../services/api';
import { toast } from 'react-toastify';
import { useNavigate, useParams } from 'react-router-dom';

const StoreForm = ({ mode = 'create' }) => {
  const navigate = useNavigate();
  const { id } = useParams();
  const [loading, setLoading] = useState(false);
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    address: '',
    city: '',
    country: '',
    phone: '',
    isDefault: false,
    isActive: true
  });

  useEffect(() => {
    if (mode === 'edit' && id) {
      fetchStore();
    }
  }, [mode, id]);

  const fetchStore = async () => {
    try {
      const { data } = await api.get(`/admin/stores/${id}`);
      setFormData(data);
    } catch (error) {
      toast.error('Error al cargar tienda');
      navigate('/admin/stores');
    }
  };

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);

    try {
      if (mode === 'create') {
        await api.post('/admin/stores', formData);
        toast.success('Tienda creada exitosamente');
      } else {
        await api.put(`/admin/stores/${id}`, formData);
        toast.success('Tienda actualizada exitosamente');
      }
      navigate('/admin/stores');
    } catch (error) {
      const message = error.response?.data?.detail || 
                      error.response?.data?.errors?.[0]?.message ||
                      'Error al guardar tienda';
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="store-form-container">
      <div className="header">
        <h1>{mode === 'create' ? 'Nueva Tienda' : 'Editar Tienda'}</h1>
        <button onClick={() => navigate('/admin/stores')}>
          Cancelar
        </button>
      </div>

      <form onSubmit={handleSubmit} className="store-form">
        <div className="form-group">
          <label htmlFor="name">
            Nombre de la tienda <span className="required">*</span>
          </label>
          <input
            type="text"
            id="name"
            name="name"
            value={formData.name}
            onChange={handleChange}
            required
            minLength={3}
            placeholder="Ej: Tienda Centro"
          />
        </div>

        <div className="form-group">
          <label htmlFor="code">
            Código (opcional)
          </label>
          <input
            type="text"
            id="code"
            name="code"
            value={formData.code}
            onChange={handleChange}
            placeholder="Ej: TC-001"
          />
          <small>Debe ser único si se proporciona</small>
        </div>

        <div className="form-group">
          <label htmlFor="address">Dirección</label>
          <input
            type="text"
            id="address"
            name="address"
            value={formData.address}
            onChange={handleChange}
            placeholder="Calle Principal 123"
          />
        </div>

        <div className="form-row">
          <div className="form-group">
            <label htmlFor="city">Ciudad</label>
            <input
              type="text"
              id="city"
              name="city"
              value={formData.city}
              onChange={handleChange}
              placeholder="Ciudad de México"
            />
          </div>

          <div className="form-group">
            <label htmlFor="country">País</label>
            <input
              type="text"
              id="country"
              name="country"
              value={formData.country}
              onChange={handleChange}
              placeholder="México"
            />
          </div>
        </div>

        <div className="form-group">
          <label htmlFor="phone">Teléfono</label>
          <input
            type="tel"
            id="phone"
            name="phone"
            value={formData.phone}
            onChange={handleChange}
            placeholder="+52 55 1234 5678"
          />
        </div>

        <div className="form-group">
          <label className="checkbox-label">
            <input
              type="checkbox"
              name="isDefault"
              checked={formData.isDefault}
              onChange={handleChange}
            />
            <span>Establecer como tienda predeterminada</span>
          </label>
          <small>Solo puede haber una tienda predeterminada</small>
        </div>

        {mode === 'edit' && (
          <div className="form-group">
            <label className="checkbox-label">
              <input
                type="checkbox"
                name="isActive"
                checked={formData.isActive}
                onChange={handleChange}
                disabled={formData.isDefault}
              />
              <span>Tienda activa</span>
            </label>
            {formData.isDefault && (
              <small className="text-warning">
                No se puede desactivar la tienda predeterminada
              </small>
            )}
          </div>
        )}

        <div className="form-actions">
          <button 
            type="button" 
            onClick={() => navigate('/admin/stores')}
            className="btn-secondary"
          >
            Cancelar
          </button>
          <button 
            type="submit" 
            disabled={loading}
            className="btn-primary"
          >
            {loading ? 'Guardando...' : mode === 'create' ? 'Crear Tienda' : 'Guardar Cambios'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default StoreForm;
```

### 3. ProductStockByStores Component

Gestión de stock por tienda para un producto.

```jsx
// src/components/admin/products/ProductStockByStores.jsx
import React, { useState, useEffect } from 'react';
import api from '../../../services/api';
import { toast } from 'react-toastify';

const ProductStockByStores = ({ productId }) => {
  const [stockData, setStockData] = useState([]);
  const [loading, setLoading] = useState(true);
  const [editingStore, setEditingStore] = useState(null);
  const [newStock, setNewStock] = useState(0);

  useEffect(() => {
    fetchStock();
  }, [productId]);

  const fetchStock = async () => {
    setLoading(true);
    try {
      const { data } = await api.get(`/admin/stores/products/${productId}/stock`);
      setStockData(data);
    } catch (error) {
      toast.error('Error al cargar stock');
    } finally {
      setLoading(false);
    }
  };

  const handleUpdateStock = async (storeId, currentStock) => {
    setEditingStore(storeId);
    setNewStock(currentStock);
  };

  const handleSaveStock = async (storeId) => {
    try {
      await api.put(`/admin/stores/products/${productId}/stock`, {
        storeId,
        stock: parseInt(newStock)
      });
      toast.success('Stock actualizado');
      setEditingStore(null);
      fetchStock(); // Recargar
    } catch (error) {
      toast.error(error.response?.data?.detail || 'Error al actualizar');
    }
  };

  const handleCancelEdit = () => {
    setEditingStore(null);
    setNewStock(0);
  };

  if (loading) return <div>Cargando stock...</div>;

  return (
    <div className="product-stock-by-stores">
      <h3>Stock por Tienda</h3>

      {stockData.length === 0 ? (
        <div className="empty-state">
          <p>Este producto no tiene stock registrado en tiendas</p>
          <p>Usa el botón "Actualizar" para asignar stock a una tienda</p>
        </div>
      ) : (
        <table className="stock-table">
          <thead>
            <tr>
              <th>Tienda</th>
              <th>Stock Total</th>
              <th>Reservado</th>
              <th>Disponible</th>
              <th>Última Actualización</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {stockData.map(item => (
              <tr key={item.id}>
                <td>
                  <strong>{item.storeName}</strong>
                </td>
                <td>
                  {editingStore === item.storeId ? (
                    <input
                      type="number"
                      min="0"
                      value={newStock}
                      onChange={(e) => setNewStock(e.target.value)}
                      className="stock-input"
                      autoFocus
                    />
                  ) : (
                    item.stock
                  )}
                </td>
                <td className="text-warning">
                  {item.reservedStock}
                </td>
                <td className="text-success">
                  <strong>{item.availableStock}</strong>
                </td>
                <td>
                  {new Date(item.updatedAt).toLocaleString()}
                </td>
                <td>
                  {editingStore === item.storeId ? (
                    <div className="action-buttons">
                      <button 
                        onClick={() => handleSaveStock(item.storeId)}
                        className="btn-sm btn-success"
                      >
                        Guardar
                      </button>
                      <button 
                        onClick={handleCancelEdit}
                        className="btn-sm btn-secondary"
                      >
                        Cancelar
                      </button>
                    </div>
                  ) : (
                    <button 
                      onClick={() => handleUpdateStock(item.storeId, item.stock)}
                      className="btn-sm"
                    >
                      Actualizar
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div className="stock-legend">
        <p><strong>Stock Total:</strong> Cantidad total en la tienda</p>
        <p><strong>Reservado:</strong> Stock en órdenes en proceso</p>
        <p><strong>Disponible:</strong> Stock - Reservado (disponible para venta)</p>
      </div>
    </div>
  );
};

export default ProductStockByStores;
```

### 4. StoreSelector Component (Para Checkout)

Selector de tienda en el checkout - **OPCIONAL**.

```jsx
// src/components/checkout/StoreSelector.jsx
import React, { useState, useEffect } from 'react';
import api from '../../services/api';
import { toast } from 'react-toastify';

const StoreSelector = ({ cartItems, onStoreSelect }) => {
  const [stores, setStores] = useState([]);
  const [selectedStore, setSelectedStore] = useState(null);
  const [stockAvailability, setStockAvailability] = useState({});
  const [loading, setLoading] = useState(true);
  const [enableStoreSelection, setEnableStoreSelection] = useState(false);

  useEffect(() => {
    fetchStores();
  }, []);

  useEffect(() => {
    if (selectedStore && enableStoreSelection) {
      checkStockForAllItems();
    }
  }, [selectedStore, cartItems]);

  const fetchStores = async () => {
    try {
      const { data } = await api.get('/admin/stores', {
        params: { includeInactive: false }
      });
      setStores(data);
      
      // Auto-seleccionar tienda default si existe
      const defaultStore = data.find(s => s.isDefault);
      if (defaultStore && !enableStoreSelection) {
        setSelectedStore(defaultStore.id);
        onStoreSelect(defaultStore.id);
      }
    } catch (error) {
      console.error('Error al cargar tiendas', error);
      // No mostrar error, el sistema puede funcionar sin tiendas
    } finally {
      setLoading(false);
    }
  };

  const checkStockForAllItems = async () => {
    const availability = {};
    
    for (const item of cartItems) {
      try {
        const { data } = await api.post(
          `/admin/stores/products/${item.productId}/check-stock`,
          {
            productId: item.productId,
            quantity: item.quantity,
            storeId: selectedStore
          }
        );
        availability[item.productId] = data;
      } catch (error) {
        availability[item.productId] = {
          isAvailable: false,
          message: 'Error al verificar stock'
        };
      }
    }
    
    setStockAvailability(availability);
  };

  const handleStoreChange = (storeId) => {
    setSelectedStore(storeId);
    onStoreSelect(storeId);
  };

  const handleToggleStoreSelection = () => {
    setEnableStoreSelection(!enableStoreSelection);
    if (!enableStoreSelection) {
      // Activar: buscar tienda default
      const defaultStore = stores.find(s => s.isDefault);
      if (defaultStore) {
        handleStoreChange(defaultStore.id);
      }
    } else {
      // Desactivar: usar stock legacy
      setSelectedStore(null);
      onStoreSelect(null);
      setStockAvailability({});
    }
  };

  // Si no hay tiendas, no mostrar nada
  if (!loading && stores.length === 0) {
    return null;
  }

  const hasInsufficientStock = Object.values(stockAvailability).some(
    v => !v.isAvailable
  );

  return (
    <div className="store-selector">
      <div className="selector-header">
        <h3>Punto de Retiro</h3>
        <label className="toggle-label">
          <input
            type="checkbox"
            checked={enableStoreSelection}
            onChange={handleToggleStoreSelection}
          />
          <span>Recoger en tienda</span>
        </label>
      </div>

      {enableStoreSelection && (
        <>
          <div className="stores-list">
            {stores.map(store => (
              <label 
                key={store.id} 
                className={`store-option ${selectedStore === store.id ? 'selected' : ''}`}
              >
                <input
                  type="radio"
                  name="store"
                  value={store.id}
                  checked={selectedStore === store.id}
                  onChange={() => handleStoreChange(store.id)}
                />
                <div className="store-info">
                  <div className="store-name">
                    <strong>{store.name}</strong>
                    {store.isDefault && (
                      <span className="badge">Predeterminada</span>
                    )}
                  </div>
                  {store.address && (
                    <p className="store-address">
                      📍 {store.address}
                      {store.city && `, ${store.city}`}
                    </p>
                  )}
                  {store.phone && (
                    <p className="store-phone">📞 {store.phone}</p>
                  )}
                </div>
              </label>
            ))}
          </div>

          {selectedStore && Object.keys(stockAvailability).length > 0 && (
            <div className={`stock-availability ${hasInsufficientStock ? 'has-errors' : ''}`}>
              <h4>Disponibilidad</h4>
              {cartItems.map(item => {
                const availability = stockAvailability[item.productId];
                if (!availability) return null;

                return (
                  <div 
                    key={item.productId} 
                    className={`availability-item ${!availability.isAvailable ? 'unavailable' : ''}`}
                  >
                    <span>{item.productName}</span>
                    {availability.isAvailable ? (
                      <span className="badge badge-success">
                        ✓ Disponible ({availability.availableStock})
                      </span>
                    ) : (
                      <span className="badge badge-danger">
                        ✗ Stock insuficiente ({availability.availableStock} disponibles)
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          )}

          {hasInsufficientStock && (
            <div className="alert alert-danger">
              ⚠️ Algunos productos no tienen stock suficiente en esta tienda.
              Selecciona otra tienda o ajusta las cantidades.
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default StoreSelector;
```

---

## 📡 API Endpoints y Ejemplos

### Base URL
```
http://localhost:5093/admin/stores
```

### 1. GET /admin/stores - Listar Tiendas

**Query Params:**
- `includeInactive` (bool, opcional): Incluir tiendas inactivas. Default: `false`

**Request:**
```javascript
const response = await api.get('/admin/stores', {
  params: { includeInactive: false }
});
```

**Response 200:**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Tienda Centro",
    "code": "TC-001",
    "address": "Calle Principal 123",
    "city": "Ciudad de México",
    "country": "México",
    "phone": "+52 55 1234 5678",
    "isDefault": true,
    "isActive": true,
    "createdAt": "2026-01-15T10:00:00Z",
    "updatedAt": "2026-01-16T14:30:00Z"
  }
]
```

### 2. GET /admin/stores/{id} - Obtener Tienda

**Request:**
```javascript
const response = await api.get(`/admin/stores/${storeId}`);
```

**Response 200:** StoreDto

**Errores:**
- `404`: Tienda no encontrada

### 3. POST /admin/stores - Crear Tienda

**Request Body:**
```javascript
const response = await api.post('/admin/stores', {
  name: "Tienda Sur",
  code: "TS-003", // Opcional
  address: "Av. Revolución 789",
  city: "Ciudad de México",
  country: "México",
  phone: "+52 55 9876 5432",
  isDefault: false
});
```

**Response 201:** StoreDto creado

**Errores:**
- `400`: Código duplicado
- `400`: Validación (nombre mínimo 3 caracteres)
- `403`: Sin permiso `inventory:create`

### 4. PUT /admin/stores/{id} - Actualizar Tienda

**Request Body:**
```javascript
const response = await api.put(`/admin/stores/${storeId}`, {
  name: "Tienda Centro - Actualizada",
  code: "TC-001",
  address: "Calle Principal 123, Col. Centro",
  city: "Ciudad de México",
  country: "México",
  phone: "+52 55 1234 5678",
  isDefault: true,
  isActive: true
});
```

**Response 200:** StoreDto actualizado

**Errores:**
- `400`: No se puede desactivar tienda default
- `400`: Código duplicado
- `404`: Tienda no encontrada

### 5. DELETE /admin/stores/{id} - Eliminar Tienda

**Request:**
```javascript
const response = await api.delete(`/admin/stores/${storeId}`);
```

**Response 204:** No Content

**Errores:**
- `400`: "Cannot delete the default store"
- `400`: "Cannot delete store with associated stock"
- `400`: "Cannot delete store with associated orders"
- `404`: Tienda no encontrada

**⚠️ Recomendación**: Mejor desactivar (`isActive: false`) que eliminar.

### 6. POST /admin/stores/{id}/set-default - Establecer Default

**Request:**
```javascript
const response = await api.post(`/admin/stores/${storeId}/set-default`);
```

**Response 200:** StoreDto con `isDefault: true`

### 7. GET /admin/stores/products/{productId}/stock - Stock por Tiendas

**Request:**
```javascript
const response = await api.get(`/admin/stores/products/${productId}/stock`);
```

**Response 200:**
```json
[
  {
    "id": "stock-record-1",
    "productId": "product-123",
    "storeId": "store-456",
    "storeName": "Tienda Centro",
    "stock": 100,
    "reservedStock": 5,
    "availableStock": 95,
    "updatedAt": "2026-01-16T15:30:00Z"
  }
]
```

### 8. PUT /admin/stores/products/{productId}/stock - Actualizar Stock

**Request Body:**
```javascript
const response = await api.put(`/admin/stores/products/${productId}/stock`, {
  storeId: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  stock: 150
});
```

**Response 200:** ProductStoreStockDto

**Comportamiento:**
- Si no existe → **crea** el registro
- Si existe → **actualiza** el stock
- `reservedStock` se mantiene intacto

### 9. POST /admin/stores/products/{productId}/check-stock - Verificar Stock

**Request Body:**
```javascript
const response = await api.post(
  `/admin/stores/products/${productId}/check-stock`,
  {
    productId: "product-123",
    quantity: 10,
    storeId: "store-456" // null para usar stock legacy
  }
);
```

**Response 200:**
```json
{
  "isAvailable": true,
  "availableStock": 95,
  "message": "Stock available: 95",
  "storeId": "store-456",
  "usedLegacyStock": false
}
```

### 10. POST /admin/stores/migrate-legacy-stock - Migrar Stock

**Request Body:**
```javascript
const response = await api.post('/admin/stores/migrate-legacy-stock', {
  defaultStoreId: "3fa85f64-5717-4562-b3fc-2c963f66afa6"
});
```

**Response 200:**
```json
{
  "migratedProductsCount": 45,
  "targetStoreId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Successfully migrated stock for 45 products"
}
```

### 11. POST /checkout/place-order - Checkout con Tienda (MODIFICACIÓN)

**⚠️ NO ROMPER**: `storeId` es **OPCIONAL**.

**Request Body:**
```javascript
const response = await api.post('/checkout/place-order', {
  idempotencyKey: "unique-key-123",
  shippingAddress: "Dirección de envío",
  email: "customer@example.com",
  phone: "+1234567890",
  paymentMethod: "CARD",
  storeId: "3fa85f64-5717-4562-b3fc-2c963f66afa6" // ⬅️ OPCIONAL
});
```

**Comportamiento:**
- `storeId: null` o ausente → Usa stock legacy (como siempre)
- `storeId: <guid>` → Usa stock de esa tienda

---

## 🔄 Flujos de Usuario

### Flujo 1: Admin crea primera tienda y migra stock

```
1. Admin entra a /admin/stores
2. Sistema muestra "No hay tiendas registradas"
3. Admin hace clic en "Crear primera tienda"
4. Llena formulario:
   - Nombre: "Almacén Principal"
   - Código: "AP-001"
   - isDefault: true (automático si es la primera)
5. Sistema crea tienda (POST /admin/stores)
6. Sistema muestra opción "Migrar stock legacy"
7. Admin confirma migración
8. Sistema migra productos con stock > 0 a la tienda
9. Éxito: "45 productos migrados"
```

### Flujo 2: Admin actualiza stock en tienda específica

```
1. Admin entra a /admin/products/{id} (producto específico)
2. Hace clic en tab "Stock por Tienda"
3. Sistema muestra lista de tiendas con stock actual
4. Admin hace clic en "Actualizar" en "Tienda Centro"
5. Aparece input editable
6. Admin cambia stock de 100 a 150
7. Admin hace clic en "Guardar"
8. Sistema valida y actualiza (PUT /admin/stores/products/{id}/stock)
9. Éxito: Toast "Stock actualizado"
10. Lista se recarga automáticamente
```

### Flujo 3: Cliente hace checkout con selección de tienda

```
1. Cliente agrega productos al carrito
2. Cliente entra a /checkout
3. Sistema muestra selector "Recoger en tienda" (checkbox)
4. Cliente activa checkbox
5. Sistema carga tiendas activas (GET /admin/stores?includeInactive=false)
6. Cliente selecciona "Tienda Norte"
7. Sistema verifica stock para cada producto del carrito
8. Sistema muestra disponibilidad:
   - ✓ Producto A: Disponible (50 unidades)
   - ✗ Producto B: Stock insuficiente (2 de 5 requeridas)
9. Cliente ajusta cantidad de Producto B o selecciona otra tienda
10. Cliente completa checkout
11. Sistema envía storeId en PlaceOrderRequest
12. Backend reserva y decrementa stock de esa tienda
```

### Flujo 4: Tenant sin tiendas (no romper nada)

```
1. Tenant no tiene tiendas creadas
2. Admin ve /admin/stores → "No hay tiendas"
3. Admin puede ignorar y seguir usando el sistema
4. Checkout funciona normalmente SIN selector de tienda
5. Órdenes se crean con storeId: null
6. Backend usa Product.Stock (legacy) automáticamente
7. Todo funciona como antes ✅
```

---

## ⚠️ Manejo de Errores

### Errores Comunes y Cómo Manejarlos

```javascript
// src/utils/errorHandler.js
export const handleApiError = (error, defaultMessage = 'Error en la operación') => {
  if (!error.response) {
    return 'Error de conexión con el servidor';
  }

  const { status, data } = error.response;

  switch (status) {
    case 400:
      // Errores de validación
      if (data.errors && Array.isArray(data.errors)) {
        return data.errors.map(e => e.message).join(', ');
      }
      return data.detail || data.message || 'Datos inválidos';

    case 401:
      localStorage.removeItem('authToken');
      window.location.href = '/login';
      return 'Sesión expirada';

    case 403:
      return 'No tienes permisos para realizar esta acción';

    case 404:
      return 'Recurso no encontrado';

    case 409:
      return 'Conflicto: el recurso ya existe';

    case 500:
      return 'Error interno del servidor';

    default:
      return defaultMessage;
  }
};

// Uso en componentes
try {
  await api.post('/admin/stores', formData);
  toast.success('Tienda creada');
} catch (error) {
  const message = handleApiError(error, 'Error al crear tienda');
  toast.error(message);
}
```

### Validaciones del Frontend

```javascript
// src/utils/storeValidations.js
export const validateStoreForm = (formData) => {
  const errors = {};

  if (!formData.name || formData.name.trim().length < 3) {
    errors.name = 'El nombre debe tener al menos 3 caracteres';
  }

  if (formData.code && formData.code.trim().length === 0) {
    errors.code = 'El código no puede estar vacío si se proporciona';
  }

  if (formData.phone && !/^[\d\s\+\-\(\)]+$/.test(formData.phone)) {
    errors.phone = 'Formato de teléfono inválido';
  }

  return Object.keys(errors).length > 0 ? errors : null;
};
```

---

## 🧪 Testing Recomendado

### Test 1: Verificar que sin tiendas todo funciona

```javascript
// tests/integration/checkout-without-stores.test.js
describe('Checkout sin tiendas', () => {
  beforeEach(() => {
    // Mock: GET /admin/stores retorna []
    cy.intercept('GET', '/admin/stores*', []);
  });

  it('debe permitir checkout sin selector de tienda', () => {
    cy.visit('/checkout');
    
    // No debe mostrar selector de tienda
    cy.get('.store-selector').should('not.exist');
    
    // Debe poder completar checkout normalmente
    cy.get('[data-testid="complete-checkout"]').click();
    
    // Verificar que NO se envía storeId
    cy.wait('@placeOrder').its('request.body').should('not.have.property', 'storeId');
  });
});
```

### Test 2: CRUD de tiendas

```javascript
describe('Gestión de tiendas', () => {
  it('debe crear una tienda', () => {
    cy.visit('/admin/stores');
    cy.contains('Nueva Tienda').click();
    
    cy.get('input[name="name"]').type('Tienda Test');
    cy.get('input[name="code"]').type('TT-001');
    cy.get('form').submit();
    
    cy.contains('Tienda creada exitosamente');
    cy.url().should('include', '/admin/stores');
  });

  it('debe mostrar error si código está duplicado', () => {
    cy.intercept('POST', '/admin/stores', {
      statusCode: 400,
      body: { detail: 'Store code already exists' }
    });
    
    cy.visit('/admin/stores/new');
    cy.get('input[name="code"]').type('TC-001');
    cy.get('form').submit();
    
    cy.contains('Store code already exists');
  });
});
```

### Test 3: Selector de tienda en checkout

```javascript
describe('Selector de tienda en checkout', () => {
  beforeEach(() => {
    cy.intercept('GET', '/admin/stores*', {
      body: [
        { id: '1', name: 'Tienda A', isDefault: true },
        { id: '2', name: 'Tienda B', isDefault: false }
      ]
    });
  });

  it('debe auto-seleccionar tienda default', () => {
    cy.visit('/checkout');
    
    cy.get('input[type="checkbox"]').contains('Recoger en tienda').check();
    
    // Debe auto-seleccionar tienda con isDefault: true
    cy.get('input[value="1"]').should('be.checked');
  });

  it('debe verificar stock al cambiar tienda', () => {
    cy.intercept('POST', '*/check-stock', {
      body: { isAvailable: false, availableStock: 0 }
    });
    
    cy.visit('/checkout');
    cy.get('input[value="2"]').check();
    
    cy.contains('Stock insuficiente');
  });
});
```

---

## 📋 Checklist de Implementación

### Admin Panel - Tiendas

- [ ] Página de lista de tiendas (`/admin/stores`)
  - [ ] GET /admin/stores implementado
  - [ ] Mostrar tiendas con badge "Predeterminada"
  - [ ] Filtro "Incluir inactivas"
  - [ ] Botón "Nueva Tienda"
  - [ ] Estado vacío cuando no hay tiendas

- [ ] Formulario de creación (`/admin/stores/new`)
  - [ ] POST /admin/stores implementado
  - [ ] Validación frontend (nombre min 3 chars)
  - [ ] Checkbox "Predeterminada"
  - [ ] Manejo de errores (código duplicado)

- [ ] Formulario de edición (`/admin/stores/:id/edit`)
  - [ ] GET /admin/stores/:id implementado
  - [ ] PUT /admin/stores/:id implementado
  - [ ] Checkbox "Activa" (disabled si es default)
  - [ ] Pre-cargar datos existentes

- [ ] Acciones sobre tiendas
  - [ ] DELETE /admin/stores/:id implementado
  - [ ] Confirmación antes de eliminar
  - [ ] POST /admin/stores/:id/set-default implementado
  - [ ] Manejo de errores (no eliminar default, con stock, con órdenes)

### Admin Panel - Stock

- [ ] Vista de stock por producto
  - [ ] GET /admin/stores/products/:id/stock implementado
  - [ ] Tabla con: Tienda, Stock, Reservado, Disponible
  - [ ] Indicador visual de stock bajo
  - [ ] Estado vacío cuando no hay stock

- [ ] Actualización de stock
  - [ ] PUT /admin/stores/products/:id/stock implementado
  - [ ] Input inline con validación (min 0)
  - [ ] Botones Guardar/Cancelar
  - [ ] Recarga automática después de guardar

- [ ] Migración de stock legacy (opcional)
  - [ ] POST /admin/stores/migrate-legacy-stock implementado
  - [ ] Botón visible cuando hay stock legacy
  - [ ] Confirmación antes de migrar
  - [ ] Mensaje de éxito con cantidad migrada

### Checkout (Opcional)

- [ ] Selector de tienda
  - [ ] Checkbox "Recoger en tienda"
  - [ ] GET /admin/stores (solo activas)
  - [ ] Auto-selección de tienda default
  - [ ] No mostrar si no hay tiendas

- [ ] Verificación de stock
  - [ ] POST /admin/stores/products/:id/check-stock
  - [ ] Verificar todos los productos del carrito
  - [ ] Mostrar disponibilidad por producto
  - [ ] Bloquear checkout si hay stock insuficiente

- [ ] Integración con checkout existente
  - [ ] Agregar `storeId` a PlaceOrderRequest (opcional)
  - [ ] NO romper checkout existente
  - [ ] Funciona sin `storeId` (backward compatible)

### Seguridad y Permisos

- [ ] Verificar permisos `inventory:*`
- [ ] Ocultar botones según permisos
- [ ] Redirigir si no tiene acceso
- [ ] Manejar 403 Forbidden correctamente

### UX/UI

- [ ] Estados de carga (spinners)
- [ ] Toasts de éxito/error
- [ ] Validación de formularios
- [ ] Estados vacíos con mensajes claros
- [ ] Confirmaciones antes de acciones destructivas
- [ ] Responsive design

### Testing

- [ ] Test: Sistema funciona sin tiendas
- [ ] Test: CRUD de tiendas
- [ ] Test: Actualización de stock
- [ ] Test: Selector de tienda en checkout
- [ ] Test: Manejo de errores

---

## 🎨 Estilos CSS Sugeridos

```css
/* src/styles/stores.css */

/* Lista de tiendas */
.stores-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.5rem;
  margin-top: 1rem;
}

.store-card {
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  padding: 1.5rem;
  background: white;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  transition: box-shadow 0.2s;
}

.store-card:hover {
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
}

.store-card.inactive {
  opacity: 0.6;
  background: #f9fafb;
}

.store-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.badge {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
}

.badge-primary {
  background: #3b82f6;
  color: white;
}

.badge-danger {
  background: #ef4444;
  color: white;
}

.badge-success {
  background: #10b981;
  color: white;
}

/* Formulario */
.store-form {
  max-width: 600px;
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: #374151;
}

.form-group input,
.form-group select,
.form-group textarea {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font-size: 1rem;
}

.form-group input:focus {
  outline: none;
  border-color: #3b82f6;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.required {
  color: #ef4444;
}

/* Tabla de stock */
.stock-table {
  width: 100%;
  border-collapse: collapse;
  margin-top: 1rem;
}

.stock-table th {
  background: #f9fafb;
  padding: 0.75rem;
  text-align: left;
  font-weight: 600;
  border-bottom: 2px solid #e5e7eb;
}

.stock-table td {
  padding: 0.75rem;
  border-bottom: 1px solid #e5e7eb;
}

.stock-table tr:hover {
  background: #f9fafb;
}

.text-success {
  color: #10b981;
}

.text-warning {
  color: #f59e0b;
}

.text-danger {
  color: #ef4444;
}

/* Selector de tienda */
.store-selector {
  margin: 1.5rem 0;
  padding: 1.5rem;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
}

.store-option {
  display: flex;
  align-items: start;
  padding: 1rem;
  border: 2px solid #e5e7eb;
  border-radius: 8px;
  margin-bottom: 0.75rem;
  cursor: pointer;
  transition: all 0.2s;
}

.store-option:hover {
  border-color: #3b82f6;
  background: #f0f9ff;
}

.store-option.selected {
  border-color: #3b82f6;
  background: #eff6ff;
}

.store-option input[type="radio"] {
  margin-right: 1rem;
  margin-top: 0.25rem;
}

/* Estados vacíos */
.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: #6b7280;
}

.empty-state p {
  margin-bottom: 1rem;
}

/* Alertas */
.alert {
  padding: 1rem;
  border-radius: 6px;
  margin: 1rem 0;
}

.alert-danger {
  background: #fee2e2;
  border: 1px solid #fecaca;
  color: #991b1b;
}

.alert-success {
  background: #d1fae5;
  border: 1px solid #a7f3d0;
  color: #065f46;
}
```

---

## 🚀 Despliegue y Notas Finales

### Consideraciones de Producción

1. **Caché**: Las listas de tiendas cambian poco, considera caché de 5-10 min
2. **Imágenes**: Si agregas fotos de tiendas, usar CDN
3. **Performance**: Lazy load del selector de tiendas en checkout
4. **Analytics**: Trackear uso de selección de tienda

### Variables de Entorno

```env
# .env
REACT_APP_API_URL=https://api.tudominio.com
REACT_APP_ENABLE_STORES=true # Feature flag
```

### Feature Flag (Opcional)

Si quieres lanzar gradualmente:

```javascript
// src/config/features.js
export const features = {
  stores: process.env.REACT_APP_ENABLE_STORES === 'true'
};

// Uso en componentes
import { features } from '../config/features';

{features.stores && <StoreSelector />}
```

---

## 📞 Soporte

Si tienes dudas sobre:
- **Endpoints**: Revisar [LOYALTY_API_GUIDE.md](./LOYALTY_API_GUIDE.md) como referencia
- **Arquitectura backend**: Ver [STORES_DESIGN.md](./STORES_DESIGN.md)
- **Errores 400/403/404**: Verificar headers (Authorization, X-Tenant-Slug)

---

## ✅ Resumen Ejecutivo

**Lo que VAS a hacer:**
1. ✅ Admin panel para CRUD de tiendas
2. ✅ Vista de stock por producto por tienda
3. ✅ (Opcional) Selector de tienda en checkout

**Lo que NO VAS a tocar:**
1. ❌ NO modificar checkout existente (solo agregar componente opcional)
2. ❌ NO cambiar flujos de pedidos actuales
3. ❌ NO romper productos sin tiendas

**Backward Compatibility Garantizada:**
- Sistema funciona 100% sin tiendas
- Checkout funciona sin `storeId`
- Productos con stock legacy funcionan igual

**Tiempo Estimado:**
- Admin panel tiendas: 6-8 horas
- Stock por tienda: 4-6 horas
- Selector checkout: 3-4 horas
- Testing: 2-3 horas
- **Total**: 15-21 horas

---

**¡Éxito con la implementación! 🚀**
