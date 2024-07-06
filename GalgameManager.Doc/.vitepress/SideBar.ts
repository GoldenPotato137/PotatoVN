import {Options, generateSidebar} from 'vitepress-sidebar';

const vitepressSidebarOptions: Options[] = [
    {
        scanStartPath : 'usage',
        resolvePath : '/usage/',
        useTitleFromFileHeading: true,
        useFolderTitleFromIndexFile: true,
        sortMenusByFrontmatterOrder: true,
    },
    {
        scanStartPath : 'development',
        resolvePath : '/development/',
        useTitleFromFileHeading: true,
        useFolderTitleFromIndexFile: true,
        sortMenusByFrontmatterOrder: true,
        collapseDepth: 2,
    },
];

export default generateSidebar(vitepressSidebarOptions);