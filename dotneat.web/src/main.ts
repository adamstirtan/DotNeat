import { createApp } from 'vue'
import { createVuetify } from 'vuetify'
import 'vuetify/styles'
import './assets/main.css'
import App from './App.vue'

const vuetify = createVuetify()

createApp(App).use(vuetify).mount('#app')
