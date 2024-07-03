import { defineConfig } from 'vitepress'

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: "PotatoVN",
  description: "PotatoVN Official Website",
  head: [
    ['link', {rel: 'icon', href: '/favicon-16x16.png', sizes:'16x16'}],
    ['link', {rel: 'icon', href: '/favicon-32x32.png', sizes:'32x32'}],
  ],
  lang: 'zh-CN',
  lastUpdated: true,
  markdown: {
    math: true,
    container: {
      tipLabel: '提示',
      warningLabel: '警告',
      dangerLabel: '危险',
      infoLabel: '信息',
      detailsLabel: '详细信息',
      noteLabel: '为什么',
    },
  },
  themeConfig: {
    // https://vitepress.dev/reference/default-theme-config
    logo: "/avatar.png",
    nav: [
      { text: '主页', link: '/' },
      { text: '使用指北', link: '/usage' },
      { text: '一起开发', link: '/development' },
    ],
    search: { provider: 'local' },
    sidebar: {
      '/usage/': [
        {
          text: '快速启动',
          items: [
            { text: '下载与安装', link: '/usage/install' },
          ]
        },
        {
          text: '使用指北',
          items: [
            { text: '简介', link: '/usage/' },
          ]
        }
      ],
      '/development/': [
        {
          text: 'Config',
          items: [
            { text: 'Index', link: '/config/' },
            { text: 'Three', link: '/config/three' },
            { text: 'Four', link: '/config/four' }
          ]
        }
      ]
    },
    socialLinks: [
      { icon: 'github', link: 'https://github.com/GoldenPotato137/PotatoVN' }
    ],
    docFooter: {
      prev: '上一页',
      next: '下一页',
    },
    footer:{
      message: '<a href="https://beian.miit.gov.cn/">桂ICP备20002051号-2</a>',
      copyright: 'Power by <a href="https://vitepress.dev/">VitePress</a>  |  Copyright © 2024 PotatoVN.net  | <a href="https://github.com/GoldenPotato137/PotatoVN?tab=Apache-2.0-1-ov-file#readme">Apache2 License</a>',
    }
  }
})
